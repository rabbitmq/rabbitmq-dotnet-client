#!/usr/bin/env python2.5
# -*- python -*-
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

from __future__ import generators
import web
import Core
import Store
import RenderUtils
import Config
import base64
import pickle
import User
import hmac
import urllib
import os
import re
import time

urls = (
    '/([^/]*)', 'read',
    '/([^/]*)/print', 'printmode',
    '/([^/]*)/history', 'history',
    '/([^/]*)/diff', 'diff',
    '/([^/]*)/backlinks', 'backlinks',
    '/([^/]*)/subscribe', 'subscribe',
    '/([^/]*)/edit', 'edit',
    '/([^/]*)/preview', 'preview',
    '/([^/]*)/save', 'save',
    '/([^/]*)/delete', 'delete',
    '/([^/]*)/chown', 'chown',
    '/([^/]*)/mediacache/(.*)', 'mediacache',
    '/([^/]*)/attach/(.*)', 'getattach',
    '/([^/]*)/attach', 'editattach',
    '/([^/]*)/updateattach', 'updateattach',
    '/([^/]*)/delattach', 'delattach',
    '/_/static/([^/]+)', 'static',
    '/_/settings', 'settings',
    '/_/follow_backlink', 'follow_backlink',
    '/_/logout', 'logout',
    '/_/search', 'search',
    '/_/changes', 'changes',
    '/_/sitemap', 'sitemap',
    )

def mac(str):
    return hmac.new(Config.session_passphrase, str).hexdigest()

def newSession():
    return web.storage({
        'username': None,
        'ip': web.ctx.ip,
        'issuetime': time.time(),
        })

class LoginPage(Core.Renderable):
    def __init__(self, action, login_failed):
        self.action = action
        self.login_failed = login_failed

    def templateName(self):
        return 'action_loginpage'

class AuthorisationFailurePage(Core.Renderable):
    def __init__(self, action):
        self.action = action
        self.user = action.user()

    def templateName(self):
        return 'action_authorisationfailurepage'

class Action(Core.Renderable):
    def __init__(self):
        self.loadCookies_()
        self.ctx = web.ctx
        self.recoverSession_()
        self.input = web.input(**self.defaultInputs())
        self.ctx.store = Store.Transaction(Config.file_store)
        self.ctx.cache = Store.Transaction(Config.cache_store)
        self.ctx.attachments = Store.Transaction(Config.attachment_store)
        self.ctx.printmode = False
        if Config.use_canonical_base_url:
            self.ctx.home = Config.canonical_base_url

    def defaultInputs(self):
        return {
            'format': 'html'
            }

    def loadCookies_(self):
        self.cookies = web.cookies(pyle_session = '')

    def recoverSession_(self):
        self.session = None
        try:
            if self.cookies.pyle_session:
                (cookiehash, sessionpickle64) = self.cookies.pyle_session.split('::', 1)
                sessionpickle = base64.decodestring(sessionpickle64)
                computedhash = mac(sessionpickle)
                if computedhash == cookiehash:
                    self.session = pickle.loads(sessionpickle)
        except:
            pass
        self.checkSession_()
        if not self.session:
            self.session = newSession()
        self.ctx.pyleuser = None

    def checkSession_(self):
        if self.session and self.session.get("ip", '') != web.ctx.ip:
            self.session = None
        if self.session and Config.session_expiry_time != -1:
            issuetime = self.session.get("issuetime", 0)
            now = time.time()
            if now - issuetime >= Config.session_expiry_time:
                self.session = None
            else:
                self.session.issuetime = now

    def user(self):
        if not self.ctx.pyleuser:
            self.ctx.pyleuser = User.lookup(self.session.username)
        return self.ctx.pyleuser

    def ensure_login(self):
        self.ctx.pyleuser = None

        login_failed = 0
        if self.input.has_key('Pyle_username'):
            username = self.input.Pyle_username
            password = self.input.Pyle_password
            user = User.lookup(username)
            if Config.user_authenticator.authenticate(user, password):
                self.session.username = username
                self.ctx.pyleuser = user
                return True
            login_failed = 1

        web.header('Content-Type','text/html; charset=utf-8', unique=True)
        web.output(LoginPage(self, login_failed).render('html'))
        return False

    def reply_with_authorisation_failure(self):
        web.header('Content-Type','text/html; charset=utf-8', unique=True)
        web.output(AuthorisationFailurePage(self).render('html'))

    def ensure_authorised(self, *args):
        if self.is_authorised(self.user(), *args):
            # If we're authorised already, even though we may be
            # anonymous, we're good to go with presenting the page the
            # user asked for.
            return True

        if not self.user().is_anonymous():
            # If we're not authorised, and we're not the anonymous
            # user, then there's nothing more to be done. Let the user
            # know they haven't the right permissions, and skip
            # presenting the content they asked for.
            self.reply_with_authorisation_failure()
            return False

        if not self.ensure_login():
            # ensure_login returns false if it had to ask the user to
            # prove who they are, or true if it accepted their
            # credentials.
            return False

        # At this point, our self.user() has changed: we've just
        # logged in, and we're not anonymous, so recheck our
        # authorisation, because we might be allowed to do what the
        # user asked for now.

        self.saveSession() # so we're not asked to log in over and over

        if self.is_authorised(self.user(), *args):
            return True

        self.reply_with_authorisation_failure()
        return False

    def is_authorised(self, user, *args):
        return True

    def commit(self):
        self.saveSession()
        self.ctx.store.commit()
        self.ctx.cache.commit()
        self.ctx.attachments.commit()

    def render(self, format):
        self.commit()
        return Core.Renderable.render(self, format)

    def saveSession(self):
        sessionpickle = pickle.dumps(self.session)
        computedhash = mac(sessionpickle)
        web.setcookie('pyle_session',
                      computedhash + '::' + base64.encodestring(sessionpickle).strip())

    def GET(self, *args):
        if self.ensure_authorised(*args):
            self.handle_request(*args)
            self.commit()

    def POST(self, *args):
        return self.GET(*args)

    def handle_request(self, *args):
        if self.input.format == 'html':
            web.header('Content-Type','text/html; charset=utf-8', unique=True)
        web.output(self.render(self.input.format))

class PageAction(Action):
    def init_page(self, pagename):
        if not hasattr(self, 'page') or not self.page:
            if not pagename:
                pagename = Config.frontpage
            self.pagename = pagename
            if self.input.has_key('version'):
                version = self.input.version
            else:
                version = None
            self.page = Core.Page(pagename, version)

    def is_authorised(self, user, pagename, *more_args):
        self.init_page(pagename)
        return self.page.readable_for(user)

    def handle_request(self, pagename):
        self.init_page(pagename)
        Action.handle_request(self)

class EditPageAction(PageAction):
    def is_authorised(self, user, pagename):
        self.init_page(pagename)
        return self.page.writable_for(user)

class read(PageAction):
    def templateName(self):
        return 'action_read'

class printmode(PageAction):
    def prerender(self, format):
        self.ctx.printmode = True

    def templateName(self):
        return 'action_read'

class history(PageAction):
    def templateName(self):
        return 'action_history'

class diff(Action):
    def is_authorised(self, user, pagename):
        # Revisit later: what about permissions on old versions of pages??
        page = Core.Page(pagename)
        return page.readable_for(user)

    def handle_request(self, pagename):
        key = pagename + '.txt'
        msgenc = self.ctx.store.message_encoder()
        v1 = self.input.v1
        if self.input.has_key('v2'):
            v2 = self.input.v2
        else:
            v2 = msgenc.gethistory(key)[0].version_id
        self.v1 = msgenc.gethistoryentry(key, v1)
        self.v2 = msgenc.gethistoryentry(key, v2)
        self.diff = msgenc.diff(key, v1, v2)
	self.pagetitle = pagename
        Action.handle_request(self)

    def templateName(self):
        return 'action_diff'

class backlinks(PageAction):
    def prerender(self, format):
        self.backlinks = self.page.backlinks()

    def templateName(self):
        return 'action_backlinks'

class mediacache(PageAction):
    def handle_request(self, pagename, cachepath):
        self.init_page(pagename)
        (mimetype, bytes) = self.page.mediacache()[cachepath]
        web.header('Content-Type', mimetype)
        web.output(bytes)

class subscribe(PageAction):
    def is_authorised(self, user, *args):
        return not user.is_anonymous()

    def handle_request(self, pagename):
        self.init_page(pagename)
        self.subscription_status = not self.user().is_subscribed_to(pagename)
        self.user().set_subscription(pagename, self.subscription_status)
        self.user().save_properties()
        PageAction.handle_request(self, pagename)

    def templateName(self):
        return 'action_subscribe'

class edit(EditPageAction):
    def templateName(self):
        return 'action_edit'

class preview(PageAction):
    def defaultInputs(self):
        i = PageAction.defaultInputs(self)
        i['version'] = 'preview' ## needed to stop the cache being stomped on
        return i

    def handle_request(self, pagename):
        self.init_page(pagename)
        self.page.setbody(self.input.body)
        PageAction.handle_request(self, pagename)

    def templateName(self):
        return 'action_preview'

class save(EditPageAction):
    def handle_request(self, pagename):
        self.init_page(pagename)
        self.page.setbody(self.input.body)
        change_record = self.page.save(self.user(), self.input.get('oldversion', None))
        self.commit()
        self.page.post_save_hooks(change_record)
        web.seeother(RenderUtils.internal_link_url(self.page.title))

class delete(EditPageAction):
    def handle_request(self, pagename):
        if self.input.get('delete_confirmed', ''):
            self.init_page(pagename)
            self.page.delete(self.user())
            web.seeother(RenderUtils.internal_link_url(self.page.title))
        else:
            PageAction.handle_request(self, pagename)

    def templateName(self):
        return 'action_delete'

class chown(PageAction):
    def is_authorised(self, user, pagename, *more_args):
        self.init_page(pagename)
        return user.is_wheel() or (self.page.owner and self.page.owner == user.getusername())

    def handle_request(self, pagename):
        self.init_page(pagename)
        if self.user().is_wheel():
            self.page.owner = self.input.newowner or None
        self.page.viewgroup = self.input.viewgroup or None
        self.page.editgroup = self.input.editgroup or None
        self.page.save(self.user(), None)
        PageAction.handle_request(self, pagename)

    def templateName(self):
        return 'action_chown'

class getattach(PageAction):
    def handle_request(self, pagename, attachname):
        self.init_page(pagename)
        a = self.page.get_attachment(attachname, self.input.get('version', None))
        web.header('Content-Type', a.mimetype)
        web.header('Content-Length', len(a.body()))
        web.output(a.body())

class editattach(EditPageAction):
    def templateName(self):
        return 'action_editattach'

class updateattach(EditPageAction):
    def handle_request(self, pagename):
        self.init_page(pagename)

        attachname = self.input.name
        content = self.input.content
        if content and not attachname:
            attachname = os.path.basename(web.input(content = {}).content.filename)

        a = self.page.get_attachment(attachname, None)
        a.mimetype = self.input.mimetype
        a.creator = self.user().getusername()
        if content:
            a.setbody(content)
        a.save()
        self.commit()
        self.page.reset_cache()
        web.seeother(RenderUtils.internal_link_url(pagename, 'attach'))

class delattach(EditPageAction):
    def handle_request(self, pagename):
        self.attachname = self.input.name
        if self.input.get('delete_confirmed', ''):
            self.init_page(pagename)
            a = self.page.get_attachment(self.attachname, None)
            a.delete()
            self.page.reset_cache()
            web.seeother(RenderUtils.internal_link_url(pagename, 'attach'))
        else:
            PageAction.handle_request(self, pagename)

    def templateName(self):
        return 'action_delattach'

class static:
    def GET(self, filename):
        if filename in ['.', '..', '']:
            web.ctx.status = '403 Forbidden'
        else:
            f = open(os.path.join('static', filename), 'rb')
            web.output(f.read())
            f.close()

class settings(Action):
    def is_authorised(self, user, *args):
        return not user.is_anonymous()

    def handle_request(self):
        self.changes_saved = False
        if self.input.has_key('action'):
            if self.input.action == 'save_settings':
                i = web.input(email = self.user().email,
                              unsubscribe = [])
                self.user().email = i.email
                self.user().subscriptions = [s for s in self.user().subscriptions
                                             if s not in i.unsubscribe]
                self.user().save_properties()
                self.changes_saved = True
        Action.handle_request(self)

    def templateName(self):
        return 'action_settings'

class follow_backlink(Action):
    def handle_request(self):
        web.seeother(RenderUtils.internal_link_url(self.input.page))

class logout(Action):
    def handle_request(self):
        self.session.username = None
        self.ctx.pyleuser = None
        Action.handle_request(self)

    def templateName(self):
        return 'action_logout'

class search(Action):
    def handle_request(self):
        self.keywords = [k for k in self.input.get('q', '').split(' ') if k]
        if self.keywords:
            self.ran_search = True
            (self.total_result_count, self.result_groups) = self.run_search()
        else:
            self.ran_search = False
            self.total_result_count = 0
            self.result_groups = []
        Action.handle_request(self)

    def run_search(self):
        regexes = [re.compile(re.escape(k), re.IGNORECASE) for k in self.keywords]
        result = {}
        total_result_count = 0
        msgenc = self.ctx.store.message_encoder()
        for key in msgenc.keys_glob('*.txt'):
            pagetitle = key[:-4] # chop off '.txt'
            text = msgenc.getbody(key, None)
            if text is not None:
                score = 0
                headerscore = 0
                keyword_count = 0
                for r in regexes:
                    headerscore = len(r.findall(pagetitle))
                    score_increment = len(r.findall(text)) + headerscore
                    score = score + score_increment
                    if score_increment:
                        keyword_count = keyword_count + 1
                if score:
                    if not result.has_key(keyword_count): result[keyword_count] = []
                    result[keyword_count].append((score, bool(headerscore), pagetitle))
                    total_result_count = total_result_count + 1
        for group in result.values():
            group.sort(None, lambda r: r[0], True) # sort by overall score
        result = list(result.items())
        result.sort(None, lambda r: r[0], True) # sort groups by keyword_count
        return (total_result_count, result)

    def templateName(self):
        return 'action_search'

class changes(Action):
    def __init__(self):
        Action.__init__(self)
        self.recentchanges = Core.RecentChanges(None)

    def prerender(self, format):
        self.recentchanges.prerender(format)
        deltas = []
        for change in self.recentchanges.changes:
            if change.has_key('page'):
                pagename = change['page']
                page = Core.Page(pagename)
                if page.readable_for(User.anonymous):
                    deltas.append(change)
        deltas.reverse()
        self.deltas = deltas
        return Action.prerender(self, format)

    def defaultInputs(self):
        d = Action.defaultInputs(self)
        d['format'] = 'atom'
        return d

    def templateName(self):
        return 'action_changes'

    def handle_request(self, *args):
        if self.input.format == 'atom':
            web.header('Content-Type','application/atom+xml; charset=utf-8', unique=True)
        return Action.handle_request(self, *args)

class sitemap(Action):
    def render(self, format):
        cached_output = self.ctx.cache.getpickle('sitemap', None)
        if not cached_output:
            import Flatten
            (self.root_name, self.root_node, self.orphans) = \
                             Flatten.flatten_wiki(Config.flattened_root)
            cached_output = str(Action.render(self, format))
            self.ctx.cache.setpickle('sitemap', cached_output)
        return cached_output

    def templateName(self):
        return 'action_sitemap'

if __name__ == '__main__':
    Core.init_pyle()
    web.run(urls, globals())
