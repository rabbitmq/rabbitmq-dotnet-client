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

import exceptions
import traceback
import os
import cgi
import glob

def load_plugin(category, name,
                load_problem_marker = None,
                import_error_marker = None,
                other_error_marker = None):
    try:
        mod = __import__(category + '.' + name)
        if hasattr(mod, name):
            return getattr(mod, name)
        else:
            return load_problem_marker
    except exceptions.ImportError:
        return import_error_marker
    except:
        import sys
        sys.stderr.write(traceback.format_exc())
        return other_error_marker
        
def find_plugin(category, name, entrypoint):
    mod = load_plugin(category, name, 'load_problem', 'import_error', 'other_error')
    if mod == 'load_problem':
        return ('Plugin ' + category + '.' + name + \
                ' did not load correctly', None)
    if mod == 'import_error':
	return ('Could not find plugin ' + category + '.' + name, None)
    if mod == 'other_error':
	return ('Error loading plugin ' + category + '.' + name + ':\n<tt>' +
		cgi.escape(''.join(traceback.format_exception(*sys.exc_info()))) +
		'</tt>', None)
    if not hasattr(mod, entrypoint):
        return ('Plugin ' + category + '.' + name + \
                ' missing entrypoint ' + entrypoint, None)
    return (None, getattr(mod, entrypoint))

def add_default(h, key, v):
    if not h.has_key(key):
        h[key] = v
    return h

def custom_description(plugin, c, pr, sp, po):
    stanza = dict(getattr(plugin, 'info', {})) # dict() makes a copy
    stanza['keyword'] = plugin.__name__.split('.')[-1]

    add_default(stanza, 'summary', '')
    add_default(stanza, 'details', '')
    add_default(stanza, 'friendly_name', stanza['keyword'])
    add_default(stanza, 'plugin_category', c)
    add_default(stanza, 'example_prefix', pr + stanza['keyword'])
    add_default(stanza, 'example_spacing', sp)
    add_default(stanza, 'example_template', '...')
    add_default(stanza, 'example_postfix', po)

    return stanza

def spanhandler_description(plugin):
    return custom_description(plugin, 'spanhandler', '[', ' ', ']')

def sublanguage_description(plugin):
    return custom_description(plugin, 'sublanguage', '\n\n@', '\n  ', '\n')

def all_plugins(category):
    modulenames = [os.path.split(pyfile)[1][:-3]
                   for pyfile in glob.glob(os.path.join(category, "*.py"))]
    modulenames = [n for n in modulenames if n and n[0] != '_']
    modules = []
    for name in modulenames:
        mod = load_plugin(category, name)
        if mod:
            modules.append(mod)
    return modules
