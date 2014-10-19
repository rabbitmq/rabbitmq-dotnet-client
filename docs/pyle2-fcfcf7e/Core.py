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
import web
import cgi
import Block
import os
import Cheetah.Template
import Cheetah.Filters
import exceptions
import traceback
import sys
import time
import rfc822
import User
import Group
import re
import Store
import Plugin
import sets

def skinfile(file):
    p = file
    for dir in Config.skin:
        p = os.path.join(dir, file)
        if os.path.exists(p):
            return p
    # None exist. Return the last in the list for error-reporting purposes.
    return p

class Renderable:
    def render(self, format):
        self.prerender(format)
	import RenderUtils
	templatename = skinfile(self.templateName() + '.' + format)
        extra = web.storage({
            'Config': Config,
            'ctx': web.ctx,
            'skinfile': skinfile,
            })
	return Cheetah.Template.Template(file = templatename,
					 searchList = (self, RenderUtils, extra),
                                         filter = Cheetah.Filters.EncodeUnicode)

    def prerender(self, format):
        pass

    def notify_parent(self, newparent):
        pass

    def anchor(self):
        return str(self.uuid())

    def uuid(self):
        try:
            return self.uuid
        except exceptions.AttributeError:
            import uuid
            self.uuid = uuid.uuid4()
            return self.uuid

class Paragraph(Renderable):
    def __init__(self, blockPara):
	import Inline
	self.fragments = []
	Inline.parse(blockPara.as_string(), self.fragments)

    def templateName(self):
	return 'pyle_paragraph'

class Container(Renderable):
    def __init__(self, klass = ''):
	self.klass = klass
	self.container_items = []

    def addItem(self, item):
	self.container_items.append(item)
        item.notify_parent(self)

    def templateName(self):
	return 'pyle_container'

class List(Container):
    def __init__(self, is_ordered):
        Container.__init__(self)
	self.is_ordered = is_ordered

    def templateName(self):
	return 'pyle_list'

class ListItem(Paragraph, Container):
    def __init__(self, blockPara, klass = ''):
        Paragraph.__init__(self, blockPara)
        Container.__init__(self, klass)

    def templateName(self):
	return 'pyle_listitem'

class SectionHeading(Paragraph):
    def templateName(self):
        return 'pyle_sectionheading'

class Section(Container):
    def __init__(self, rank, titlepara):
	Container.__init__(self)
	self.rank = rank
        self.tocpath = []
        self.subsectioncount = 0
        if titlepara:
            self.heading = SectionHeading(titlepara)
        else:
            self.heading = None

    def subsections(self):
        return [x for x in self.container_items if isinstance(x, Section)]

    def notify_parent(self, newparent):
        self.tocpath = newparent.alloc_toc_entry()

    def alloc_toc_entry(self):
        self.subsectioncount = self.subsectioncount + 1
        entry = self.tocpath[:]
        entry.append(self.subsectioncount)
        return entry

    def anchor(self):
        return 'section_' + '_'.join([str(part) for part in self.tocpath])

    def templateName(self):
	return 'pyle_section'

class Separator(Renderable):
    def templateName(self):
	return 'pyle_separator'

class BlockSyntaxError(Exception): pass

class PyleBlockParser(Block.BasicWikiMarkup):
    def __init__(self, page):
	Block.BasicWikiMarkup.__init__(self, self)
	self.page = page
	self.accumulator = page
	self.stack = []
        self.rank_offset = 0

    def current_rank(self):
	if isinstance(self.accumulator, Section):
	    return self.accumulator.rank - self.rank_offset
	else:
	    return 0

    def save_rank_offset(self):
        old_offset = self.rank_offset
        self.rank_offset = self.rank_offset + self.current_rank()
        return old_offset

    def restore_rank_offset(self, old_offset):
        self.rank_offset = old_offset

    def push_acc(self, acc):
	self.accumulator.addItem(acc)
	self.stack.append(self.accumulator)
	self.accumulator = acc
	return acc

    def add(self, item):
	self.accumulator.addItem(item)

    def pop_acc(self):
	self.accumulator = self.stack.pop()

    def push_visit_pop(self, container, kids):
	self.push_acc(container)
	self.visit(kids)
	self.pop_acc()

    def begin_list(self, is_ordered):
	self.push_acc(List(is_ordered))

    def begin_listitem(self, para):
        self.push_acc(ListItem(para))

    def end_listitem(self):
        self.pop_acc()

    def end_list(self, is_ordered):
	self.pop_acc()

    def visit_section(self, rank, titlepara, doc):
	while self.current_rank() >= rank:
	    self.pop_acc()
#	while self.current_rank() < rank - 1:
#	    self.push_acc(Section(self.current_rank() + 1, None))
	self.push_acc(Section(rank + self.rank_offset, titlepara))

    def visit_separator(self):
	self.add(Separator())

    def visit_sublanguage(self, commandline, doc):
	commandparts = commandline.split(' ', 1)
	command = commandparts[0]
	if len(commandparts) > 1:
	    args = commandparts[1]
	else:
	    args = ''
	(err, plugin) = Plugin.find_plugin('sublanguages', command, 'SublanguageHandler')
	if plugin:
	    try:
		plugin(args, doc, self)
            except BlockSyntaxError, e:
                import Inline
                self.add(Inline.MarkupError(True, 'pluginerror', e.message))
	    except:
                import traceback
                import Inline
                tb = traceback.format_exc()
		self.add(Inline.MarkupError(True, 'pluginerror', tb))
                sys.stderr.write(tb)
	else:
	    import Inline
	    self.add(Inline.MarkupError(True, 'missingsublanguage', err))

    def visit_normal(self, para):
	self.add(Paragraph(para))

class DefaultPageContent(Renderable):
    def __init__(self, title):
        self.title = title

    def templateName(self):
        return 'default_page_content'

class PageChangeEmail(Renderable):
    def __init__(self, page, change_record):
        self.page = page
        self.change_record = change_record

    def templateName(self):
        return 'page_change'

def send_emails(users, message_including_headers):
    if users and Config.smtp_hostname:
        import smtplib
        s = smtplib.SMTP(Config.smtp_hostname, Config.smtp_portnumber)
        s.sendmail(Config.daemon_email_address, [u.email for u in users],
                   normalize_newlines(message_including_headers).replace('\n', '\r\n'))
        s.quit()

def normalize_newlines(s):
    s = s.replace('\r\n', '\n')
    s = s.replace('\r', '\n')
    return s

def log_change(d):
    if not d.get('when', None):
        d['when'] = time.time()
    oldchanges = web.ctx.cache.getpickle('changes', [])
    oldchanges.append(d)
    web.ctx.cache.setpickle('changes', oldchanges)

class RecentChanges(Renderable):
    def __init__(self, count):
        self.count = count

    def prerender(self, format):
        self.changes = web.ctx.cache.getpickle('changes', [])
        if self.count is not None:
            self.changes = self.changes[-self.count:]

    def changes_by_day(self):
        result = []
        group = []
        previoustime = time.gmtime(0)
        def pushgroup():
            if group:
                group.sort(None, lambda c: c.get('when', 0))
                result.append((previoustime, group))
        for change in self.changes:
            when = change.get('when', 0)
            eventtime = time.gmtime(when)
            if previoustime[:3] != eventtime[:3]:
                pushgroup()
                group = []
            previoustime = eventtime
            group.append(change)
        pushgroup()
        return result

    def changes_by_day_newest_first(self):
        result = self.changes_by_day()
        result.reverse()
        for (when, group) in result:
            group.reverse()
        return result

    def templateName(self):
        return 'pyle_recentchanges'

class Attachment(Store.Item):
    def __init__(self, pagetitle, name, version):
        Store.Item.__init__(self,
                            web.ctx.attachments,
                            pagetitle + '.attach.' + name,
                            version)
        self.pagetitle = pagetitle
        self.name = name

    def compute_bodylen(self):
        self.body()
        return self.bodylen

    default_properties = {
        'mimetype': 'application/octet-stream',
        'author': '',
        'bodylen': compute_bodylen  # an unclosed method!
        }

    def body(self):
        if not self._body:
            Store.Item.body(self)
            self.bodylen = str(len(self._body))
        return self._body

    def setbody(self, newbody):
        self._body = newbody
        self.bodylen = str(len(self._body))

    def save(self):
        self.body()
        self.primitive_save()

    def delete(self):
        self.primitive_delete()

    def url(self):
        return web.ctx.home + '/' + self.pagetitle + '/attach/' + self.name

class Page(Section, Store.Item):
    def __init__(self, title, version = None):
        Store.Item.__init__(self, web.ctx.store, title + '.txt', version)
        Section.__init__(self, 0, None)
        self.title = title
	self.cache = web.ctx.cache
        self.notify_required = False
        self.container_items = None
        self._mediacache = None
        self._rendercache = None

    default_properties = {
        'timestamp': rfc822.formatdate(0),
        'author': '',
        'owner': None,
        'viewgroup': None,
        'editgroup': None,
        }

    def __getstate__(self):
        return {'title': self.title, 'version': self.version}

    def __setstate__(self, state):
        self.__init__(state['title'], state['version'])

    def body(self):
        if self._body is None:
            if self.version or self.exists():
                return Store.Item.body(self)
            else:
                self._body = str(DefaultPageContent(self.title).render('txt'))
        return self._body

    def timestamp_epoch(self):
        return time.mktime(rfc822.parsedate(self.timestamp))

    def _preprocess(self, s):
        return normalize_newlines(s)

    def setbody(self, newtext):
        newtext = self._preprocess(newtext)
        self.notify_required = self.notify_required or (self._body != newtext)
	self._body = newtext
        self.reset_cache()

    def prerender(self, format):
        if not self.version:
            self.container_items = self.cache.getpickle(self.title + '.tree', None)
            self._mediacache = self.cache.getpickle(self.title + '.mediacache', {})
            self._rendercache = self.cache.getpickle(self.title + '.rendercache', {})
            if self.container_items is not None:
                return

        self.container_items = []
        self._mediacache = {}
        self._rendercache = {}

        if hasattr(web.ctx, 'active_page'):
            old_active_page = web.ctx.active_page
        else:
            old_active_page = None
        try:
            web.ctx.active_page = self
            self.render_on(PyleBlockParser(self))
        finally:
            web.ctx.active_page = old_active_page

        if not self.version:
            self.cache.setpickle(self.title + '.tree', self.container_items)
            self.cache.setpickle(self.title + '.mediacache', self._mediacache)
            self.cache.setpickle(self.title + '.rendercache', self._rendercache)

    def render_on(self, renderer):
        if hasattr(web.ctx, 'source_page_title'):
            old_source_page_title = web.ctx.source_page_title
        else:
            old_source_page_title = None
        try:
            web.ctx.source_page_title = self.title
            doc = Block.parsestring(self.body())
            return renderer.visit(doc.children)
        finally:
            web.ctx.source_page_title = old_source_page_title

    def mediacache(self):
        if self._mediacache is None:
            self.prerender('html')
        return self._mediacache

    def rendercache(self):
        return self._rendercache

    def save(self, user, oldversion):
        savetime = time.time()
        self.timestamp = rfc822.formatdate(savetime)
        if not self.exists():
            self.set_creation_properties(user)
        self.author = user.getusername()
        self.primitive_save()
        return self.change_record('saved', user, savetime, oldversion)

    def post_save_hooks(self, change_record):
        change_record['newversion'] = self.head_version()
        log_change(change_record)
        self.notify_subscribers(change_record)

    def set_creation_properties(self, user):
        if not user.is_anonymous():
            self.owner = user.getusername()
            self.viewgroup = user.getdefaultgroup()
            self.editgroup = user.getdefaultgroup()

    def reset_cache(self):
        reset_cache(self.cache, self.title)

    def mark_dependency_on(self, other_title):
        key = other_title + '.dependencies'
        dependencies = self.cache.getpickle(key, sets.Set())
        dependencies.add(self.title)
        self.cache.setpickle(key, dependencies)

    def delete(self, user):
        self.reset_cache()
        self.primitive_delete()
        for name in self.attachment_names():
            Attachment(self.title, name, None).delete()
        log_change(self.change_record('deleted', user))

    def attachment_names(self):
        prefix = self.title + '.attach.'
        prefixlen = len(prefix)
        return [f[prefixlen:]
                for f in web.ctx.attachments.message_encoder().keys_glob(prefix + '*')]

    def get_attachments(self):
        return [Attachment(self.title, name, None) for name in self.attachment_names()]

    def get_attachment(self, name, version):
        return Attachment(self.title, name, version)

    def backlinks(self):
        return backlinks(self.title, self.msgenc)

    def subscribers(self):
        result = []
        for username in Config.user_data_store.keys():
            user = User.User(username)
            if user.is_subscribed_to(self.title):
                result.append(user)
        return result

    def notify_subscribers(self, change_record):
        if self.notify_required:
            users = [s for s in self.subscribers() if s.username != change_record['who']]
            if users:
                notification = str(PageChangeEmail(self, change_record).render('email'))
                send_emails(users, notification)
            self.notify_required = False

    def change_record(self, event, user, when = None, oldversion = None, newversion = None):
        return {'page': self.title,
                'what': event,
                'who': user.username,
                'when': when,
                'oldversion': oldversion,
                'newversion': newversion}

    def readable_for(self, user):
        return user in Group.lookup(self.viewgroup or Config.default_view_group,
                                    User.get_wheel_group())

    def writable_for(self, user):
        return user in Group.lookup(self.editgroup or Config.default_edit_group,
                                    User.get_wheel_group())

    def templateName(self):
	return 'pyle_page'

def reset_cache(cache, initial_title):
    seen = sets.Set()
    def reset1(title):
        if title in seen:
            return
        seen.add(title)
        cache.delete(title + '.tree')
        cache.delete(title + '.mediacache')
        cache.delete(title + '.rendercache')
        dependencies = cache.getpickle(title + '.dependencies', sets.Set())
        cache.delete(title + '.dependencies')
        for dependency in dependencies:
            reset1(dependency)
    reset1(initial_title)
    cache.delete('sitemap')

def backlinks(pagename, msgenc):
    result = []
    r = re.compile(r'\b' + re.escape(pagename) + r'\b')
    for otherpage in msgenc.keys_glob('*.txt'):
        othertext = msgenc.getbody(otherpage, None)
        if r.search(othertext):
            result.append(otherpage[:-4]) # chop off the '.txt'
    result.sort()
    return result

app_initialised = 0
def init_pyle():
    global app_initialised
    if not app_initialised:
        pass
