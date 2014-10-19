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

from __future__ import nested_scopes

import re
import cgi
import Core
import Config
import RenderUtils
import Plugin

intlinkre = re.compile('(' + Config.linkpattern + ')')
extlinkre = re.compile(r'\b((http|https|ftp|mailto|gopher|news|nntp|file):[^\s\(\)]+[^\s\(\).,\'\"?])([\s.,\'\"?]?)')
italre = re.compile(r"''(.*?)''", re.S)
boldre = re.compile(r"\*(.*?)\*", re.S)
underre = re.compile(r"\b_(.*?)_\b", re.S)

def nearest_match(text, matches):
    smallestname = None
    smallest = None
    for (matchname, substring) in matches:
	pos = text.find(substring)
	if pos != -1:
	    if smallest is None or pos < smallest:
		smallestname = matchname
		smallest = pos
    return (smallestname, smallest)

class HtmlFragment(Core.Renderable):
    def __init__(self, html):
	self.html = html

    def render(self, format):
	return self.html

class TagFragment(Core.Renderable):
    def __init__(self, templateName, fragments, klass=''):
	self.templateNameVar = templateName
	self.klass = klass
	self.fragments = fragments

    def templateName(self):
	return self.templateNameVar

class LiteralFragment(Core.Renderable):
    def __init__(self, text):
	self.text = text

    def render(self, format):
	return cgi.escape(self.text)

class ExternalLink(Core.Renderable):
    def __init__(self, url, vistext):
	self.url = url
	self.vistext = vistext

    def templateName(self):
	return 'pyle_externallink'

class MarkupError(Core.Renderable):
    def __init__(self, is_block, klass, message):
	self.is_block = is_block
	self.klass = klass
	self.message = message

    def templateName(self):
	return 'pyle_markuperror'

def _appendRegexMarkup(s, result):
    while s:
	m = extlinkre.search(s)
	if m:
	    frag = s[:m.start()]
	    splice = ExternalLink(m.group(1), m.group(1))
	    s = m.group(3) + s[m.end():]
	else:
	    frag = s
	    splice = None
	    s = ''

	frag = cgi.escape(frag)
	frag = boldre.sub(r'<b>\1</b>', frag)
	frag = italre.sub(r'<i>\1</i>', frag)
	frag = underre.sub(r'<u>\1</u>', frag)

	while 1:
	    m = intlinkre.search(frag)
	    if m:
		result.append(HtmlFragment(frag[:m.start()]))
		result.append(RenderUtils.InternalLink(m.group()))
		frag = frag[m.end():]
	    else:
		result.append(HtmlFragment(frag))
		break

	if splice:
	    result.append(splice)

def _collectFragment(s):
    frag = []
    while 1:
	(matchname, min_i) = nearest_match(s, [('backslash', '\\'),
					       ('open', '['),
					       ('close', ']')])
	if matchname:
	    frag.append(s[:min_i])
	    s = s[min_i:]
	else:
	    frag.append(s)
	    s = ''

	if not s:
	    return (''.join(frag), None, '')

	if s[0] != '\\':
	    return (''.join(frag), s[0], s[1:])

	if len(s) > 1:
	    if s[1] in '[]':
		frag.append(s[1])
		s = s[2:]
	    else:
		frag.append(s[0])
		s = s[1:]
	else:
	    s = ''

def collectSpan(s):
    nesting = 0
    result = []
    while s:
	(frag, what, s) = _collectFragment(s)
	result.append(frag)
	if what is None:
	    break
	elif what == '[':
	    result.append('[')
	    nesting = nesting + 1
	elif what == ']':
	    if nesting == 0:
		break
	    else:
		result.append(']')
		nesting = nesting - 1
	else:
	    raise 'Invalid result from _collectFragment', what
    return (''.join(result), s)

def discardSpan(s):
    (text, s) = collectSpan(s)
    return s

def parse(s, result = None):
    if result is None:
	result = []

    while s:
	(frag, what, rest) = _collectFragment(s)
	_appendRegexMarkup(frag, result)
	if what is None or what == ']':
	    return (result, rest)
	elif what == '[':
	    (name, pos) = nearest_match(rest, [('spacepos', ' '),
					       ('spacepos', '\n'),
					       ('spacepos', '\r'),
					       ('spacepos', '\t'),
					       ('closepos', ']')])
	    if name == 'spacepos':
		handlername = rest[:pos]
		args = rest[pos + 1:]
	    elif name == 'closepos':
		handlername = rest[:pos]
		args = rest[pos:]
	    else:
		handlername = rest
		args = ''

	    if not handlername or not handlername[0].isalpha():
		handlername = 'invalid_spanhandler_name'
		args = rest

	    (err, plugin) = Plugin.find_plugin('spanhandlers', handlername,
                                               'SpanHandler')
	    if plugin:
		s = plugin(args, result)
	    else:
		result.append(MarkupError(False, 'missingspanhandler', err))
		s = args
	else:
	    raise 'Invalid result from _collectFragment', what

    return (result, '')
