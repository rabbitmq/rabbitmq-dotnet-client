# PYLE - A WikiClone in Python
# Copyright (C) 2004 - 2009  Tony Garnock-Jones <tonyg@kcbbs.gen.nz>
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

# This module contains a basic block-structure parser for Wiki-style
# documents, similar to the Zope StructuredText in some ways. The
# routines here are used by the renderer in Utils.py.

from __future__ import nested_scopes

import sys
import string
import re

from pprint import pprint

def indentby(amount, lines):
    prefix = ' ' * amount
    return [prefix + line for line in lines]

class Paragraph:
    def __init__(self, indent, line_number, lines):
        self.indent = indent
        self.line = line_number
        self.lines = lines

    def indented_lines(self):
        if self.indent > 0:
            return indentby(self.indent, self.lines)
        else:
            return self.lines

    def as_string(self):
        return '\n'.join(self.lines)

class Document:
    def __init__(self, para, kids):
        self.paragraph = para
        self.children = kids

    def first_child_line(self):
        if self.children:
            return self.children[0].paragraph.line
        else:
            return self.paragraph.line + len(self.paragraph.lines)

    def reconstruct_text(self, additional_indent = 0):
        r = []
        _reconstruct([self], additional_indent, r,
                     max(0, self.paragraph.indent), ## might be -1 if root doc
                     self.paragraph.line)
        return Paragraph(0, self.paragraph.line, r)

    def reconstruct_child_text(self, additional_indent = 0):
        r = []
        if self.children:
            _reconstruct(self.children, additional_indent, r,
                         min([kid.paragraph.indent for kid in self.children]),
                         self.first_child_line())
        return Paragraph(0, self.paragraph.line, r)

def _reconstruct(docs, additional_indent, lines, baseindent, linenum):
    if docs:
        for doc in docs:
            docline = doc.paragraph.line
            if docline > linenum:
                lines.extend([""] * (docline - linenum))
                linenum = docline
            lines.extend(indentby(doc.paragraph.indent + additional_indent - baseindent,
                                  doc.paragraph.lines))
            linenum = _reconstruct(doc.children,
                                   additional_indent,
                                   lines,
                                   baseindent,
                                   linenum + len(doc.paragraph.lines))
    return linenum

def parsefile(filename):
    f = open(filename, 'r')
    lines = f.readlines()
    f.close()
    return parselines(lines)

def parsestring(s):
    lines = string.split(s, '\n')
    return parselines(lines)

_ws_prefix_re = re.compile('( *)(.*)')
def parselines(lines):
    def collect_paras(lines):
        paras = []
        line_number = 1
        lineacc = []
        previndent = None

        for line in lines:
            line = string.expandtabs(string.rstrip(line, '\r\n'))
            (prefix, line) = _ws_prefix_re.match(line).groups()
            indent = len(prefix)
            if previndent is None: previndent = indent

            if previndent != indent or not line:
                if lineacc: paras.append(Paragraph(previndent, prevline_number, lineacc))
                previndent = indent
                lineacc = []

            if line:
                if not lineacc: prevline_number = line_number
                lineacc.append(line)

            line_number = line_number + 1

        if lineacc:
            paras.append(Paragraph(previndent, prevline_number, lineacc))

        return paras

    def group_paras(paras):
        stack = [Document(Paragraph(-1, 0, []), [])]
        for para in paras:
            while para.indent <= stack[-1].paragraph.indent:
                doc = stack.pop()
                stack[-1].children.append(doc)
            stack.append(Document(para, []))
        while len(stack) > 1:
            doc = stack.pop()
            stack[-1].children.append(doc)
        return stack.pop()

    paras = collect_paras(lines)
    return group_paras(paras)

def doc_to_tuple(d):
    return (d.paragraph.indent, d.paragraph.line, d.paragraph.lines, map(doc_to_tuple, d.children))

# Interface BlockRenderer:
#   def begin_list(self, is_ordered)
#   def begin_listitem(self, para)
#   def end_listitem(self)
#   def end_list(self, is_ordered)
#   def visit_section(self, rank, titleline, doc)
#   def visit_separator(self)
#   def visit_sublanguage(self, commandline, doc)
#   def visit_normal(self, para)

class BasicWikiMarkup:
    def __init__(self, visitor):
        self.visitor = visitor

    def goto_state(self, oldstate, newstate):
        if newstate != oldstate:
            if oldstate == 'ordered': self.visitor.end_list(1)
            elif oldstate == 'unordered': self.visitor.end_list(0)
            elif oldstate is None: pass
            else: raise 'Illegal BasicWikiMarkup oldstate', oldstate

            if newstate == 'ordered': self.visitor.begin_list(1)
            elif newstate == 'unordered': self.visitor.begin_list(0)
            elif newstate is None: pass
            else: raise 'Illegal BasicWikiMarkup newstate', newstate
        return newstate

    def visit(self, docs):
        self._visitkids(docs)

    def _visitkids(self, children):
        state = None
        for kid in children:
            para = kid.paragraph
            lines = para.lines
            firstline = lines[0]

            if para.indent == 0 and firstline.startswith('*'):
                numstars = 1
                while numstars < len(firstline) and firstline[numstars] == '*':
                    numstars = numstars + 1
                state = self.goto_state(state, None)
                lines[0] = firstline[numstars:].strip()
                self.visitor.visit_section(numstars, para, kid)
                self._visitkids(kid.children)
                continue

            firstchar = firstline[0]
            if firstchar == '-':
                if len(lines) == 1 and \
                   string.count(firstline, '-') == len(firstline) and \
                   len(firstline) >= 4:
                    self.visitor.visit_separator()
                else:
                    state = self.goto_state(state, 'unordered')
                    self.visit_list_item('-', kid)
            elif firstchar == '#':
                state = self.goto_state(state, 'ordered')
                self.visit_list_item('#', kid)
            elif firstchar == '@':
                state = self.goto_state(state, None)
                self.visitor.visit_sublanguage(firstline[1:].strip(), kid)
            else:
                state = self.goto_state(state, None)
                self.visitor.visit_normal(kid.paragraph)
                self._visitkids(kid.children)
        state = self.goto_state(state, None)
        return None

    # Builds a list item. Has to check for a few cases:
    #
    # --------------------
    # - this case
    #   where the first paragraph runs on in the first child
    # --------------------
    # - this case
    #
    #   where there's a gap to force a split
    # --------------------
    # - this case
    # - where there are several
    # - items to return
    # --------------------
    # - this case
    # where the first paragraph runs on
    # directly, without indent (already covered)
    # --------------------
    # - this case
    #   - where there's a nested sublist immediately
    # --------------------
    def visit_list_item(self, leader, doc):
        para = doc.paragraph
        lines = para.lines
        is_compound = 1
        for line in lines:
            if not line.startswith(leader):
                is_compound = 0
                break
        if is_compound:
            for line in lines[:-1]:
                (pos, line) = trim_item_line(line, leader)
                self.visitor.begin_listitem(Paragraph(para.indent + pos, para.line, [line]))
                self.visitor.end_listitem()
            (itempos, itemline) = trim_item_line(lines[-1], leader)
            itemrestlines = []
            itemlinenum = para.line + len(lines) - 1
        else:
            (itempos, itemline) = trim_item_line(lines[0], leader)
            itemrestlines = lines[1:]
            itemlinenum = para.line
        if doc.children:
            firstkid = doc.children[0]
        else:
            firstkid = None
        runs_on = firstkid and \
                  is_compound and \
                  firstkid.paragraph.line == para.line + len(lines) and \
                  firstkid.paragraph.indent == para.indent + itempos + 1 and \
                  firstkid.paragraph.lines[0][0] not in '-#'
        itemlines = [itemline]
        itemlines.extend(itemrestlines)
        if runs_on:
            itemlines.extend(firstkid.paragraph.lines)
            remainingkids = doc.children[1:]
        else:
            remainingkids = doc.children
        self.visitor.begin_listitem(Paragraph(para.indent + itempos, itemlinenum, itemlines))
        self._visitkids(remainingkids)
        self.visitor.end_listitem()

def trim_item_line(line, leader):
    pos = 0
    while pos < len(line) and line[pos] == leader:
        pos = pos + 1
    return (pos, line[pos:])

class TestVisitor:
    def begin_list(self, is_ordered):
        if is_ordered:
            print '<ol>'
        else:
            print '<ul>'

    def begin_listitem(self, para):
        print '<li>'
        print para.as_string()

    def end_listitem(self):
        print '</li>'

    def end_list(self, is_ordered):
        if is_ordered:
            print '</ol>'
        else:
            print '</ul>'

    def visit_section(self, rank, titlepara, doc):
        print '<h' + str(rank) + '>' + titlepara.as_string() + '</h' + str(rank) + '>'

    def visit_separator(self):
        print '<hr>'

    def visit_sublanguage(self, commandline, doc):
        print '<pre>'
        print '@ ' + commandline
        print doc.reconstruct_child_text().as_string()
        print '</pre>'

    def visit_normal(self, para):
        print '<p>' + para.as_string() + '</p>'

def _testfile(filename):
    doc = parsefile(filename)
    #pprint(doc_to_tuple(doc))

    #print doc.reconstruct_text().as_string()
    #print '-' * 75

    markup = BasicWikiMarkup(TestVisitor())
    print 'Content-type: text/html'
    print
    markup.visit(doc.children)

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print 'Missing filename'
    filename = sys.argv[1]
    _testfile(filename)
