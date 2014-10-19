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
import Group
import urllib
import re

def get_wheel_group():
    return Group.lookup(Config.wheel_group, Group.EmptyGroup())

class User:
    def __init__(self, username):
        self.username = username
        self.load_properties()

    def getusername(self):
        return self.username

    def getdefaultgroup(self):
        return self.defaultgroup

    def all_groups(self):
        if self.is_wheel():
            return Group.all_groups()
        else:
            return dict([(k,v) for (k,v) in Group.all_groups().items() if self in v])

    def is_anonymous(self):
        return False

    def is_wheel(self):
        return self in get_wheel_group()

    def email_address_editable(self):
        return True

    def load_properties(self):
        props = Config.user_data_store.getpickle(self.username, Config.default_user_properties)
        self.email = props.get('email', None)
        self.subscriptions = props.get('subscriptions', [])
        self.defaultgroup = props.get('defaultgroup', None)

    def is_subscribed_to(self, pagename):
        return pagename in self.subscriptions

    def set_subscription(self, pagename, should_subscribe):
        if should_subscribe and not self.is_subscribed_to(pagename):
            self.subscriptions.append(pagename)
        elif not should_subscribe:
            self.subscriptions = [s for s in self.subscriptions if s != pagename]
        else:
            pass

    def save_properties(self):
        props = {
            'email': self.email,
            'subscriptions': self.subscriptions,
            'defaultgroup': self.defaultgroup,
            }
        Config.user_data_store.setpickle(self.username, props)

class Anonymous(User):
    def __init__(self):
        User.__init__(self, '')

    def is_anonymous(self):
        return True

    def load_properties(self):
        self.email = None
        self.subscriptions = []
        self.defaultgroup = None

    def save_properties(self):
        pass

class Authenticator:
    def authenticate(self, user, password):
        subClassResponsibility()

    def lookup_user(self, username):
        subClassResponsibility()

class BugzillaUser(User):
    def email_address_editable(self):
        return False

    def load_properties(self):
        User.load_properties(self)
        self.email = self.username

    def save_properties(self):
        self.email = self.username
        User.save_properties(self)

class BugzillaAuthenticator(Authenticator):
    def __init__(self,
                 url = None,
                 success_regex = None,
                 default_email_suffix = None,
                 login_input = 'Bugzilla_login',
                 password_input = 'Bugzilla_password',
                 other_inputs = [('GoAheadAndLogIn', '1')]):
        self.url = url
        self.success_regex = re.compile(success_regex)
        self.default_email_suffix = default_email_suffix
        self.login_input = login_input
        self.password_input = password_input
        self.other_inputs = other_inputs

    def authenticate(self, user, password):
        if user.is_anonymous():
            return False

        inputs = [(self.login_input, user.getusername()),
                  (self.password_input, password)] + self.other_inputs
        data = urllib.urlencode(inputs)
        resulthandle = urllib.urlopen(self.url, data)
        result = resulthandle.read()
        resulthandle.close()

        if self.success_regex.search(result):
            return True
        else:
            return False

    def lookup_user(self, username):
        if username.find('@') == -1 and self.default_email_suffix:
            username = username + '@' + self.default_email_suffix
        return BugzillaUser(username)

class FilteringAuthenticator(Authenticator):
    def __init__(self, filterfunction, backing):
        self.filterfunction = filterfunction
        self.backing = backing

    def authenticate(self, user, password):
        if not self.filterfunction(user.getusername()):
            return False
        return self.backing.authenticate(user, password)

    def lookup_user(self, username):
        return self.backing.lookup_user(username)

###########################################################################

anonymous = Anonymous()

def lookup(username):
    if username is None:
        return anonymous
    else:
        return Config.user_authenticator.lookup_user(username)
