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

import web
import Core
import cgi
import urllib
import time

class InternalLink(Core.Renderable):
    def __init__(self, pagename, service = None, vistext = None, args = {}):
	if not vistext:
	    vistext = pagename

	self.pagename = pagename
	self.service = service
	self.vistext = vistext
        self.args = args

    def url(self):
        if (not self.service or self.service == None) and not self.pageexists:
            service = 'edit'
        else:
            service = self.service
        return internal_link_url(self.pagename, service, self.args)

    def prerender(self, format):
        self.pageexists = web.ctx.store.message_encoder().has_key(self.pagename + '.txt')

    def templateName(self):
	return 'pyle_internallink'

def internal_link_url(pagename, service = None, args = {}):
    if service and service != 'read':
        servicePart = '/' + service
    else:
        servicePart = ''
    queryPart = urllib.urlencode(args)
    if queryPart:
        queryPart = '?' + queryPart
    return web.ctx.home + '/' + pagename + servicePart + queryPart

def internal_link(pagename, service = None, vistext = None, format = 'html', args = {}):
    return InternalLink(pagename, service, vistext, args).render(format)

class MediaCacheEntry(InternalLink):
    def __init__(self, pagename, path, vistext, template, mimetype):
	InternalLink.__init__(self, pagename, 'mediacache/' + path, vistext)
	self.path = path
	self.template = template
        self.mimetype = mimetype

    def templateName(self):
	return self.template

def media_cache(renderer, cachepath, vistext, template, mimetype, bytes):
    renderer.page.mediacache()[cachepath] = (mimetype, bytes)
    return MediaCacheEntry(renderer.page.title, cachepath, vistext, template, mimetype)

escape = cgi.escape

def escapeall(lines):
    return map(escape, lines)

def escapeallpre(lines):
    return ''.join([escape(x).replace('\n', '&nbsp;<br />') for x in lines])

def aescape(s):
    s = escape(s)
    s = s.replace('"', '&quot;')
    s = s.replace("'", '&apos;')
    return s

def pquote(s):
    return urllib.quote_plus(s, ':/')

def quote(s):
    return urllib.quote(s, ':/')

def all_plugin_descriptions():
    import json
    import Plugin
    return json.write({"spanhandlers": [Plugin.spanhandler_description(p)
                                        for p in Plugin.all_plugins('spanhandlers')],
                       "sublanguages": [Plugin.sublanguage_description(p)
                                        for p in Plugin.all_plugins('sublanguages')]
                       })

def atomDate(unixTime):
    return time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime(unixTime))
