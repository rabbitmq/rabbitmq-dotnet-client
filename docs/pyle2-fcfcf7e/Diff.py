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

import os
import re

markerre = re.compile(r'^@@ -(\d+)(,\d+)? \+(\d+)(,\d+)? @@')

class Chunk:
    def __init__(self, linenumber1, linenumber2):
        self.linenumber1 = linenumber1
        self.linenumber2 = linenumber2
        self.header = []
        self.footer = []
        self.chunk1 = []
        self.chunk2 = []
        self.state = 0
        self.kind = None

    def extend(self, discriminator, line):
        if discriminator == ' ':
            if self.state == 0:
                self.header.append(line)
            else:
                self.state = 2
                self.footer.append(line)
        elif discriminator == '-':
            if self.state == 2:
                return True
            self.state = 1
            self.chunk1.append(line)
        elif discriminator == '+':
            if self.state == 2:
                return True
            self.state = 1
            self.chunk2.append(line)
        else:
            pass
        return False

    def finish(self):
        if self.chunk1 and self.chunk2:
            self.kind = 'change'
        elif self.chunk1:
            self.kind = 'deletion'
        elif self.chunk2:
            self.kind = 'insertion'
        else:
            self.kind = 'nothing'

    def reverse(self):
        t = self.chunk1
        self.chunk1 = self.chunk2
        self.chunk2 = t
        t = self.linenumber1
        self.linenumber1 = self.linenumber2
        self.linenumber2 = t
        self.finish()

class Diff:
    def __init__(self, title, v1, v2, difflines):
        self.title = title
        self.v1 = v1
        self.v2 = v2
        self.chunks = []
        self.chunk = None
        self.parse_result(difflines)

    def finish_chunk(self):
        if self.chunk:
            self.chunk.finish()
            self.chunks.append(self.chunk)
            self.chunk = None

    def parse_result(self, result):
        for line in result:
            markermatch = markerre.search(line)
            if markermatch:
                self.finish_chunk()
                self.chunk = Chunk(markermatch.group(1), markermatch.group(3))
            elif self.chunk:
                if self.chunk.extend(line[0], line[1:]):
                    self.finish_chunk()
                    self.chunk = Chunk(None, None)
                    self.chunk.extend(line[0], line[1:])
        self.finish_chunk()

    def reverse(self):
        t = self.v1
        self.v1 = self.v2
        self.v2 = t
        for chunk in self.chunks:
            chunk.reverse()
