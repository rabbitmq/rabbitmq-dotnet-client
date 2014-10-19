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

import re

class Group:
    def __init__(self):
        pass

    def __contains__(self, user):
        self.subClassResponsibility()

    def __and__(self, other): return And(self, other)
    def __or__(self, other): return Or(self, other)
    def __sub__(self, other): return Sub(self, other)
    def __invert__(self): return Not(self)

class EmptyGroup(Group):
    def __contains__(self, user):
        return False

class Public(Group):
    def __contains__(self, user):
        return True

class Anonymous(Group):
    def __contains__(self, user):
        return user.is_anonymous()

class NameList(Group):
    def __init__(self, initial_members = []):
        Group.__init__(self)
        self.members = frozenset(initial_members)

    def __contains__(self, user):
        return user.getusername() in self.members

class Regex(Group):
    def __init__(self, username_pattern, flags = 0):
        Group.__init__(self)
        self.pattern = re.compile('^' + username_pattern + '$')

    def __contains__(self, user):
        return bool(self.pattern.match(user.getusername()))

class EmailDomain(Regex):
    def __init__(self, email_domain):
        Regex.__init__(self, '.*@' + re.escape(email_domain), re.IGNORECASE)

class Not(Group):
    def __init__(self, g):
        Group.__init__(self)
        self.inner_group = g

    def __contains__(self, user):
        return user not in self.inner_group

    def __invert__(self):
        return self.inner_group

class BinaryGroup(Group):
    def __init__(self, g1, g2):
        Group.__init__(self)
        self.group1 = g1
        self.group2 = g2

class And(BinaryGroup):
    def __contains__(self, user):
        return (user in self.group1) and (user in self.group2)

class Or(BinaryGroup):
    def __contains__(self, user):
        return (user in self.group1) or (user in self.group2)

class Sub(BinaryGroup):
    def __contains__(self, user):
        return (user in self.group1) and (user not in self.group2)

def lookup(groupname, default_value = None):
    import Groups
    g = Groups.__dict__.get(groupname, None)
    if isinstance(g, Group):
        return g
    if default_value:
        return default_value
    raise 'No such group', groupname

def all_groups():
    import Groups
    return dict([(k, v) for (k, v) in Groups.__dict__.items() if isinstance(v, Group)])
