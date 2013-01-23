##  The contents of this file are subject to the Mozilla Public License
##  Version 1.1 (the "License"); you may not use this file except in
##  compliance with the License. You may obtain a copy of the License
##  at http://www.mozilla.org/MPL/
##
##  Software distributed under the License is distributed on an "AS IS"
##  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
##  the License for the specific language governing rights and
##  limitations under the License.
##
##  The Original Code is RabbitMQ.
##
##  The Initial Developer of the Original Code is VMware, Inc.
##  Copyright (c) 2007-2013 VMware, Inc.  All rights reserved.
##

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
