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

import sys
import os
import string
import re

import urllib
import xml.dom.minidom

def kids(e, name):
    return [n for n in e.childNodes if n.nodeName == name]

mimemap = {
    'image/png': ('.png', 'PNG')
    }

def main(options, pagenames):
    global mimemap
    totaldoc = xml.dom.minidom.parse(options.skeleton)
    for pagename in pagenames:
        url = options.baseurl + '/' + pagename + '?format=docbookx'
        f = urllib.urlopen(url)
        pagedoc = xml.dom.minidom.parse(f)
        f.close()
        s = totaldoc.importNode(pagedoc.documentElement, True)
        totaldoc.renameNode(s, None, 'section')
        totaldoc.documentElement.appendChild(s)
    tempcount = 0
    for graphic in totaldoc.getElementsByTagName('graphic'):
        entry = mimemap.get(graphic.getAttributeNS('http://www.eighty-twenty.org/2007/pyle2/',
                                                   'mimetype'))
        if entry:
            tempfilename = options.tempfileprefix + str(tempcount) + entry[0]
            tempcount = tempcount + 1
            tempinput = \
                urllib.urlopen(graphic.getAttributeNS('http://www.eighty-twenty.org/2007/pyle2/',
                                                      'url'))
            tempfile = open(tempfilename, 'w')
            tempfile.write(tempinput.read())
            tempfile.close()
            tempinput.close()
            newGraphic = totaldoc.createElement('graphic')
            newGraphic.setAttribute('fileref', tempfilename)
            newGraphic.setAttribute('format', entry[1])
            graphic.parentNode.replaceChild(newGraphic, graphic)
        else:
            graphic.parentNode.removeChild(graphic)
    outfile = open(options.outputfilename, 'w')
    outfile.write(totaldoc.toxml())
    outfile.close()

if __name__ == '__main__':
    from optparse import OptionParser
    parser = OptionParser('usage: %prog [options] pagename ...')
    parser.add_option("-s", "--skeleton", dest="skeleton", metavar="FILENAME")
    parser.add_option("-b", "--base-url", dest="baseurl", metavar="URL")
    parser.add_option("-o", "--output", dest="outputfilename", metavar="FILENAME")
    parser.add_option("-t", "--tempfiles", dest="tempfileprefix", metavar="FILEPREFIX")
    (options, args) = parser.parse_args()
    if not options.skeleton: parser.error('Skeleton argument required')
    if not options.baseurl: parser.error('Base URL argument required')
    if not options.outputfilename: parser.error('Output filename required')
    if not options.tempfileprefix: parser.error('Temporary filename prefix required')
    main(options, args)
