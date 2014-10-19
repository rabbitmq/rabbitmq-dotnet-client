# PYLE - A WikiClone in Python
# Copyright (C) 1999 - 2009  Tony Garnock-Jones <tonyg@kcbbs.gen.nz>
# Copyright (C) 2004 - 2009  LShift Ltd <query@lshift.net>
#
# This program is free software; you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation; either version 2 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program; if not, write to the Free Software
# Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

import Config
import Inline
import re
import sets

def flatten_wiki(chosen_root = None, dump_tree = False):
    """
    Flattens the graph of wiki pages into a tree (as contained within
    Config.file_store), using a pagerank-like algorithm to decide how
    to break the DAG into a tree, and bottom-up tree construction to
    decide how best to represent cyclic (parent-to-child) paths.

    Returns a triple:
     [0] = the name of the root page
     [1] = the tree rooted at that page
     [2] = dictionary of orphans (orphan name -> tree rooted there)

    If chosen_root is supplied, and not None, then it will be used as
    the main root of the main tree ('actual_root'). Otherwise, the
    most 'popular' page in the entire wiki is used as the root.

    Local variables:
     - outbound_links: maps page title to list of page titles
     - inbound_links: maps page title to list of page titles
     - parent: maps page title to page title (or None, for orphans)
     - children: maps page title to list of page titles
     - roots: list of page titles
     - actual_root: page title

    Each entry in both of outbound_links and inbound_links is
    eventually sorted so that the first title in the list is the page
    with the most inbound links of all the candidates. Essentially,
    this is a sorting based on a rough approximation to page
    popularity or authoritativeness, similar to what Google are doing.

    The 'parent' map finds the highest-ranked potential parent that it
    can find for each page, excluding those possibilities that lead to
    (indirect) child-parent cycles, forming for the first time a
    proper tree. Pages linked to by the page under consideration are
    considered first as potential parents; if no such page satisfies
    the 'no cycle' check, then the pages that link to the page are
    considered. Less popular nodes are inserted into 'parent' first,
    so that more popular nodes will end up closer to the root of the
    final tree.

    The tree is then re-rooted at actual_root, and the final
    representation is constructed and returned.
    """
    msgenc = Config.file_store.message_encoder()

    outbound_links = {}
    inbound_links = {}

    for filename in msgenc.keys_glob('*.txt'):
        pagename = filename[:-4] # chop off the '.txt'
        body = msgenc.getbody(filename, None)
        links = sets.Set([x[0]
                          for x in Inline.intlinkre.findall(body)
                          if msgenc.has_key(x[0] + '.txt')])
        outbound_links[pagename] = links
        for target in links:
            if not inbound_links.has_key(target):
                inbound_links[target] = sets.Set()
            inbound_links[target].add(pagename)

    if len(outbound_links) == 0:
        return None

    def sort_by_inbound_links(x, reverse = True):
        x.sort(key = lambda page: len(inbound_links.get(page, [])), reverse = reverse)

    def sort_table_by_inbound_links(t):
        for page in t.keys():
            targets = list(t[page])
            sort_by_inbound_links(targets)
            t[page] = targets

    sort_table_by_inbound_links(outbound_links)
    sort_table_by_inbound_links(inbound_links)

    pages_by_inbound_link_count = outbound_links.keys()
    sort_by_inbound_links(pages_by_inbound_link_count, reverse = False)

    parent = {}
    roots = sets.Set()

    def path_from_to(a, b):
        if a == b:
            return True
        node = a
        while node:
            p = parent.get(node, None)
            if p == b:
                return True
            node = p
        return False

    def best_parent_from(selections, page):
        for selection in selections:
            if not path_from_to(selection, page):
                return selection
        return None

    for page in pages_by_inbound_link_count:
        targets = outbound_links[page]
        bestparent = best_parent_from(targets, page)
        if not bestparent: bestparent = best_parent_from(inbound_links.get(page, []), page)

        if bestparent:
            parent[page] = bestparent
        else:
            roots.add(page)

    def reroot_at(page):
        node = page
        newparent = None
        while node:
            oldparent = parent.get(node, None)
            parent[node] = newparent
            newparent = node
            node = oldparent
        roots.remove(newparent)
        roots.add(page)

    actual_root = chosen_root or pages_by_inbound_link_count[-1]
    reroot_at(actual_root)

    children = {}
    for (c, p) in parent.items():
        if not children.has_key(p): children[p] = []
        children[p].append(c)

    def construct_tree(root):
        return dict((child, construct_tree(child)) for child in children.get(root, []))

    result = ( actual_root,
               construct_tree(actual_root),
               dict((root, construct_tree(root))
                    for root in roots
                    if root != actual_root) )
    if dump_tree:
        dump({ result[0]: result[1],
               "(Orphans)": result[2] }, 0)

    return result

def dump(node, indent):
    """
    Prints a representation of a tree-of-dictionaries. Useful for
    debugging flatten_wiki.
    """
    for (childname, childnode) in node.items():
        print '%s- %s' % ('        ' * indent, childname)
        dump(childnode, indent + 1)

if __name__ == '__main__':
    flatten_wiki(None, dump_tree = True)
