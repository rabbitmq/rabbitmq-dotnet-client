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
import pickle
import os
import glob
import fnmatch
import exceptions
import sets
import time
import Diff
import StringIO
import types

import warnings
import exceptions
warnings.filterwarnings('ignore',
                        r'.*tmpnam is a potential security risk to your program$',
                        exceptions.RuntimeWarning,
                        r'.*Store$',
                        129)
warnings.filterwarnings('ignore',
                        r'.*tmpnam is a potential security risk to your program$',
                        exceptions.RuntimeWarning,
                        r'.*Store$',
                        602)

class Store:
    def __init__(self):
        self._encoder = self.message_encoder_class(self)

    def keys(self):
        subClassResponsibility()

    def keys_glob(self, keyglob):
        subClassResponsibility()

    def has_key(self, key):
        subClassResponsibility()

    def gethistory(self, key):
        subClassResponsibility()

    def getreadstream(self, key, version):
        subClassResponsibility()

    def getwritestream(self, key):
        subClassResponsibility()

    def delete(self, key):
        subClassResponsibility()

    def message_encoder(self):
        return self._encoder

    def getbinary(self, key, defaultvalue, version = None):
        f = self.getreadstream(key, version)
        if f:
            result = f.read()
            f.close()
            return f
        else:
            return defaultvalue

    def setbinary(self, key, value):
        f = self.getwritestream(key)
        f.write(value)
        f.close()

    def getpickle(self, key, defaultvalue, version = None):
        f = self.getreadstream(key, version)
        if f:
            result = pickle.load(f)
            f.close()
            return result
        else:
            return defaultvalue

    def setpickle(self, key, value):
        f = self.getwritestream(key)
        pickle.dump(value, f)
        f.close()

    def gethistoryentry(self, key, version):
        entries = self.gethistory(key)
        for entry in entries:
            if entry.version_id == version:
                return entry
        for entry in entries:
            if entry.friendly_id == version:
                return entry
        return HistoryEntry(version, version, 0)

    def diff(self, key, v1, v2):
        text1 = self.getbinary(key, '', v1)
        text2 = self.getbinary(key, '', v2)
        f1 = tempfilewith(text1)
        f2 = tempfilewith(text2)
        command = 'diff -u3 %s %s' % (f1, f2)
        self.debug_log_cmd(command, 'diff')
        f = os.popen(command)
        result = f.readlines()
        f.close()
        os.unlink(f1)
        os.unlink(f2)
        return Diff.Diff(key, v1, v2, result)

    def process_transaction(self, changed, deleted):
        for key, value in changed.items():
            self.setbinary(key, value)
        result = []
        for key in deleted:
            keyresult = self.delete(key)
            if type(keyresult) != bool:
                raise Exception('delete() method on store %s returned non-boolean %s for key %s'
                                % (self, keyresult, key))
            if keyresult:
                result.append(key)
        return result

    def debug_log_cmd(self, command, context):
        #import sys
        #sys.stderr.write("Store.debug_log_cmd: %s: %s\n" % (context, command))
        pass

    def system(self, command, context):
        self.debug_log_cmd(command, context)
        return os.system(command)

def tempfilewith(text):
    name = os.tmpnam()
    f = open(name, 'w+')
    f.write(text)
    f.close()
    return name

def readheaders(f):
    result = {}
    while True:
        line = f.readline().rstrip('\n')
        if line == '':
            return result
        (key, value) = line.split(': ', 1)
        result[key] = value

def writeheaders(h, f):
    for (key, value) in h.iteritems():
        f.write(key)
        f.write(': ')
        f.write(value)
        f.write('\n')

class CompoundMessageEncoder:
    def __init__(self, store):
        self.store = store

    def has_key(self, key):
        return self.store.has_key(key)

    def keys_glob(self, keyglob):
        return self.store.keys_glob(keyglob)

    def getheaders(self, key, version):
        f = self.store.getreadstream(key, version)
        if f:
            result = readheaders(f)
            f.close()
            return result
        else:
            return {}

    def getmessage(self, key, version):
        f = self.store.getreadstream(key, version)
        if f:
            header = readheaders(f)
            body = f.read()
            f.close()
            return (header, body)
        else:
            return ({}, '')

    def getbody(self, key, version):
        (header, body) = self.getmessage(key, version)
        return body

    def setmessage(self, key, headers, body):
        f = self.store.getwritestream(key)
        writeheaders(headers, f)
        f.write('\n')
        f.write(body)
        f.close()

    def delete(self, key):
        self.store.delete(key)

    def gethistory(self, key):
        return self.store.gethistory(key)

    def gethistoryentry(self, key, version):
        return self.store.gethistoryentry(key, version)

    def diff(self, key, v1, v2):
        return self.store.diff(key, v1, v2)

class SplitMessageEncoder:
    def __init__(self, store):
        self.store = store

    def has_key(self, key):
        return self.store.has_key('data.' + key)

    def keys_glob(self, keyglob):
        return [k[5:] for k in self.store.keys_glob('data.' + keyglob)]

    def getheaders(self, key, version):
        f = self.store.getreadstream('meta.' + key, version)
        if f:
            result = readheaders(f)
            f.close()
            return result
        else:
            return {}

    def getmessage(self, key, version):
        return (self.getheaders(key, version), self.getbody(key, version))

    def getbody(self, key, version):
        f = self.store.getreadstream('data.' + key, version)
        if f:
            body = f.read()
            f.close()
            return body
        else:
            return ''

    def setmessage(self, key, headers, body):
        f = self.store.getwritestream('meta.' + key)
        writeheaders(headers, f)
        f.close()
        f = self.store.getwritestream('data.' + key)
        f.write(body)
        f.close()

    def delete(self, key):
        self.store.delete('meta.' + key)
        self.store.delete('data.' + key)

    def gethistory(self, key):
        return self.store.gethistory('data.' + key)

    def gethistoryentry(self, key, version):
        return self.store.gethistoryentry('data.' + key, version)

    def diff(self, key, v1, v2):
        return self.store.diff('data.' + key, v1, v2)

class HistoryEntry:
    def __init__(self, version_id, friendly_id, timestamp, next_entry = None):
        self.version_id = version_id
        self.friendly_id = friendly_id
        self.timestamp = timestamp
        self.previous = None
        self.next = next_entry
        if next_entry:
            next_entry.previous = self

    def __cmp__(self, other):
        return cmp(self.timestamp, other.timestamp)

class FileStore(Store):
    def __init__(self, dirname):
        Store.__init__(self)
        self.dirname = dirname

    message_encoder_class = SplitMessageEncoder

    def path_(self, key):
        return os.path.join(self.dirname, key)

    def keys(self):
        return self.keys_glob('*')

    def keys_glob(self, keyglob):
        globpattern = self.path_(keyglob)
        prefixlen = len(globpattern) - len(keyglob)
        return [filename[prefixlen:] for filename in glob.iglob(globpattern)]

    def has_key(self, key):
        return os.path.exists(self.path_(key))

    def current_mtime(self, key):
        try:
            return os.stat(self.path_(key)).st_mtime
        except:
            return 0

    def gethistory(self, key):
        return [HistoryEntry("current", "current", self.current_mtime(key))]

    def getreadstream(self, key, version):
        try:
            f = open(self.path_(key), 'rb')
            return f
        except IOError:
            return None

    def getwritestream(self, key):
        return open(self.path_(key), 'wb')

    def delete(self, key):
        try:
            os.unlink(self.path_(key))
            return True
        except exceptions.OSError:
            return False

def parse_cvs_timestamp(s):
    m = re.match('(\d+)[/-](\d+)[/-](\d+) +(\d+):(\d+):(\d+)( +([^ ]+))?', s)
    if m:
        parts = map(int, m.groups()[:6])
        zone = m.group(8)
        # Timezone processing and CVS are independently horrible.
        # Brought together, they're impossible.
        timestamp = time.mktime(parts + [-1, -1, -1])
    else:
        timestamp = 0
    return timestamp

def describe_transaction(changed, deleted):
    return "Wikipages changed."
#     f = StringIO.StringIO()
#     f.write("Wikipages changed.\n")
#     if changed:
#         f.write("\nPages altered or added:\n")
#         for pagename in changed:
#             f.write(" - " + pagename + "\n")
#     if deleted:
#         f.write("\nPages deleted:\n")
#         for pagename in deleted:
#             f.write(" - " + pagename + "\n")
#     return f.getvalue()

def shell_quote(key):
    return '"' + key.replace('\\', '\\\\').replace('"', '\\"') + '"'

def quote_keys(what):
    result = []
    for key in what:
        f = shell_quote(key)
        result.append(f)
    return ' '.join(result)

class SimpleShellStoreBase(FileStore):
    def __init__(self, dirname):
        FileStore.__init__(self, dirname)
        self.history_cache = {}
        self.version_cache = {}

    def ensure_history_for(self, key):
        while True:
            if not self.history_cache.has_key(key):
                self.history_cache[key] = (self.compute_history_for(key),
                                           self.current_mtime(key))
            (result, mtime) = self.history_cache[key]
            if self.current_mtime(key) == mtime:
                return result
            else:
                del self.history_cache[key]

    def pipe(self, text):
        try:
            cmd = self.shell_command(text)
            self.debug_log_cmd(cmd, 'pipe')
            return os.popen(cmd, 'r')
        except:
            return None

    def pipe_lines(self, text, nostrip = False):
        f = self.pipe(text)
        if not f:
            return []
        if nostrip:
            result = f.readlines()
        else:
            result = [x.strip() for x in f.readlines()]
        f.close()
        return result

    def pipe_all(self, text):
        f = self.pipe(text)
        if not f:
            return ''
        result = f.read()
        f.close()
        return result

    def getreadstream(self, key, version):
        if version:
            k = (key, version)
            if not self.version_cache.has_key(k):
                f = self.old_readstream(key, version)
                self.version_cache[k] = f.read()
                f.close()
            return StringIO.StringIO(self.version_cache[k])
        else:
            return FileStore.getreadstream(self, key, version)

    def getwritestream(self, key):
        self.history_cache.pop(key, None)
        return FileStore.getwritestream(self, key)

    def delete(self, key):
        self.history_cache.pop(key, None)
        return FileStore.delete(self, key)

class CvsStore(SimpleShellStoreBase):
    def __init__(self, dirname):
        SimpleShellStoreBase.__init__(self, dirname)

    message_encoder_class = CompoundMessageEncoder

    def shell_command(self, text):
        return 'cd ' + self.dirname + ' && cvs ' + text + ' 2>/dev/null'

    def compute_history_for(self, key):
        lines = self.pipe_lines('log ' + shell_quote(key))
        entries = []
        versionmap = {}
        i = 0
        entry = None
        while i < len(lines):
            if lines[i] == '----------------------------':
                revision = lines[i+1].split(' ')[1]
                fields = [[k.strip() for k in f.strip().split(':', 1)]
                          for f in lines[i+2].split(';')]
                fmap = dict([f for f in fields if len(f) == 2])
                if fmap.has_key('commitid'):
                    versionid = fmap['commitid']
                else:
                    versionid = revision
                timestamp = parse_cvs_timestamp(fmap['date'])

                versionmap[versionid] = revision
                entry = HistoryEntry(versionid, revision, timestamp, entry)
                entries.append(entry)
                i = i + 2
            i = i + 1
        return (versionmap, entries)

    def gethistory(self, key):
        (versionmap, entries) = self.ensure_history_for(key)
        return entries

    def old_readstream(self, key, version):
        (versionmap, entries) = self.ensure_history_for(key)
        if versionmap.has_key(version):
            revision = versionmap[version]
            return self.pipe('update -r ' + revision + ' -p ' + shell_quote(key)) or None
        else:
            return None

    def diff(self, key, v1, v2):
        (versionmap, entries) = self.ensure_history_for(key)
        r1 = versionmap.get(v1, v1)
        r2 = versionmap.get(v2, v2)
        return Diff.Diff(key, v1, v2,
                         self.pipe_lines('diff -u3 -r %s -r %s %s' % \
                                         (r1, r2, shell_quote(key)),
                                         True))

    def process_transaction(self, changed, deleted):
        actually_deleted = FileStore.process_transaction(self, changed, deleted)
        cmd = '( cd ' + shell_quote(self.dirname) + ' && ('
        touched = ''
        if actually_deleted:
            keys = quote_keys(actually_deleted)
            cmd = cmd + ' cvs remove ' + keys + ' ;'
            touched = touched + ' ' + keys
        if changed:
            keys = quote_keys(changed)
            cmd = cmd + ' cvs add -kb ' + keys + ' ;'
            touched = touched + ' ' + keys
        if touched:
            cmd = cmd + ' cvs commit -m ' + \
                  shell_quote(describe_transaction(changed, actually_deleted)) + \
                  touched
            cmd = cmd + ' )) >/dev/null 2>&1'
            self.system(cmd, 'process_transaction')
        return actually_deleted

class SvnStore(SimpleShellStoreBase):
    def __init__(self, dirname):
        SimpleShellStoreBase.__init__(self, dirname)
        self.load_repository_properties()

    def shell_command(self, text):
        return 'cd ' + self.dirname + ' && svn ' + text + ' 2>/dev/null'

    def load_repository_properties(self):
        self.repository_properties = self.svn_info('')

    def svn_info(self, f):
        result = {}
        for line in self.pipe_lines('info ' + f):
            parts = [part.strip() for part in line.split(':', 1)]
            if len(parts) == 2:
                result[parts[0]] = parts[1]
        return result

    def gethistory(self, key):
        return self.ensure_history_for(key)

    def compute_history_for(self, key):
        lines = self.pipe_lines('log ' + shell_quote(key))
        entries = []
        i = 0
        entry = None
        while i + 1 < len(lines):
            if lines[i] == \
                   '------------------------------------------------------------------------':
                fields = [f.strip() for f in lines[i+1].split('|')]
                if len(fields) > 1:
                    versionid = fields[0][1:]
                    timestamp = parse_cvs_timestamp(fields[2])
                    entry = HistoryEntry(versionid, versionid, timestamp, entry)
                    entries.append(entry)
            i = i + 1
        return entries

    def old_readstream(self, key, version):
        return self.pipe('cat -r ' + version + ' ' + shell_quote(key)) or None

    def diff(self, key, v1, v2):
        return Diff.Diff(key, v1, v2,
                         self.pipe_lines('diff -r %s:%s %s' % \
                                         (v1, v2, shell_quote(key)),
                                         True))

    def process_transaction(self, changed, deleted):
        actually_deleted = FileStore.process_transaction(self, changed, deleted)
        cmd = '( cd ' + self.dirname + ' && ('
        touched = ''
        if actually_deleted:
            keys = quote_keys(actually_deleted)
            cmd = cmd + ' svn delete ' + keys + ' ;'
            touched = touched + ' ' + keys
        if changed:
            keys = quote_keys(changed)
            cmd = cmd + ' svn add ' + keys + ' ;'
            touched = touched + ' ' + keys
        if touched:
            cmd = cmd + ' svn commit -m ' + \
                  shell_quote(describe_transaction(changed, actually_deleted)) + \
                  touched
            cmd = cmd + ' ; svn update )) >/dev/null 2>&1'
            self.system(cmd, 'process_transaction')
        return actually_deleted

class DarcsStore(SimpleShellStoreBase):
    def __init__(self, reporoot, repooffset = '', author_email = 'darcs-store@pyle'):
        self.reporoot = reporoot
        self.repooffset = repooffset
        SimpleShellStoreBase.__init__(self, os.path.join(reporoot, repooffset))
        self.author_email = author_email

    def shell_command(self, text):
        return 'cd ' + self.dirname + ' && darcs ' + text + ' 2>/dev/null'

    def gethistory(self, key):
        (versionmap, entries) = self.ensure_history_for(key)
        return entries

    def compute_history_for(self, key):
        import xml.dom.minidom
        f = self.pipe('changes --xml-output ' + shell_quote(key))
        dom = xml.dom.minidom.parse(f)
        f.close()
        entries = []
        versionmap = {}
        for patch in dom.getElementsByTagName("patch"):
            revision = patch.attributes['hash'].value
            date = time.strptime(patch.attributes['local_date'].value,
                                 '%a %b %d %H:%M:%S %Z %Y')
            friendly = time.strftime('%Y%m%d%H%M%S', date)
            versionmap[revision] = friendly
            entry = HistoryEntry(revision, friendly, time.mktime(date))
            entries.append(entry)
        entries.sort(reverse = True)
        nextentry = None
        linkedentries = []
        for entry in entries:
            if nextentry and nextentry.version_id == entry.version_id:
                pass
            else:
                if nextentry:
                    entry.next = nextentry
                    nextentry.previous = entry
                nextentry = entry
                linkedentries.append(entry)
        return (versionmap, linkedentries)

    def old_readstream(self, key, version):
        entry = self.gethistoryentry(key, version)
        tmpdir = os.tmpnam()
        cmd = 'darcs get --to-match="hash %s" %s %s >/dev/null' % \
              (entry.version_id, shell_quote(self.reporoot), shell_quote(tmpdir))
        self.system(cmd, 'old_readstream')
        try:
            f = open(os.path.join(tmpdir, self.repooffset, key), 'rb')
            self.system('rm -rf %s' % (shell_quote(tmpdir),), 'old_readstream')
            return f
        except IOError:
            self.system('rm -rf %s' % (shell_quote(tmpdir),), 'old_readstream')
            return StringIO.StringIO('') # file is presumably deleted at this point in time

    def diff(self, key, v1, v2):
        e1 = self.gethistoryentry(key, v1)
        e2 = self.gethistoryentry(key, v2)
        if e1 and e2 and e1 > e2:
            reversal = True
            t = e1
            e1 = e2
            e2 = t
        else:
            reversal = False
        if e1: e1 = e1.next
        if e1 and e2:
            d = Diff.Diff(key, v1, v2,
                          self.pipe_lines(('diff -u --from-match="hash %s" ' \
                                           '--to-match="hash %s" %s') \
                                          % (e1.version_id, e2.version_id, shell_quote(key)),
                                          True))
            if reversal:
                d.reverse()
            return d
        else:
            return Diff.Diff(key, v1, v2, [])

    def process_transaction(self, changed, deleted):
        actually_deleted = FileStore.process_transaction(self, changed, deleted)
        cmd = '( cd ' + self.dirname + ' && ('
        touched = ''
        if actually_deleted:
            keys = quote_keys(actually_deleted)
            touched = touched + ' ' + keys
        if changed:
            keys = quote_keys(changed)
            cmd = cmd + ' darcs add ' + keys + ' ;'
            touched = touched + ' ' + keys
        if touched:
            cmd = cmd + ' darcs record -a --author=' + shell_quote(self.author_email) + \
                  ' -m ' + \
                  shell_quote(describe_transaction(changed, actually_deleted)) + \
                  touched
            cmd = cmd + ' )) >/dev/null 2>&1'
            self.system(cmd, 'process_transaction')

class MercurialStore(SimpleShellStoreBase):
    def __init__(self, reporoot, repooffset = '', branch = 'default', author_email = 'mercurial-store@pyle'):
        self.reporoot = reporoot
        self.repooffset = repooffset
        self.branch = branch
        SimpleShellStoreBase.__init__(self, os.path.join(reporoot, repooffset))
        self.author_email = author_email

    def shell_command(self, text):
        return 'cd ' + self.dirname + ' && hg ' + text + ' 2>/dev/null'

    def gethistory(self, key):
        (versionmap, entries) = self.ensure_history_for(key)
        return entries

    def compute_history_for(self, key):
        f = self.pipe('log ' + shell_quote(key))
        lines = f.readlines()
        f.close()

        changesets = {}

        changeset = {}
        def finish_changeset():
            if len(changeset):
                if not changeset.has_key('branch'):
                    changeset['branch'] = 'default'
                if changeset['branch'] == self.branch:
                    changesets[changeset['changeset']] = dict(changeset) # take a copy
                changeset.clear()
        for line in lines:
            if line == "\n":
                finish_changeset()
            else:
                (key, value) = (s.strip() for s in line.split(":", 1))
                changeset[key] = value
        finish_changeset()

        versionmap = {}
        entries = []

        tzre = re.compile('.*([-+])([0-9]{2})([0-9]{2})')
        for changeset in changesets.values():
            (friendly, revision) = changeset['changeset'].split(":", 1)
            timestamp = 0
            if changeset.has_key('date'):
                datestr = changeset['date']
                (sign, houroffset, minuteoffset) = tzre.match(datestr).groups()
                houroffset = int(houroffset)
                minuteoffset = int(minuteoffset)
                if sign == '-':
                    houroffset = -houroffset
                    minuteoffset = -minuteoffset
                datestr = datestr[:-6]
                dateval = list(time.strptime(datestr, '%a %b %d %H:%M:%S %Y'))
                dateval[3] = dateval[3] - houroffset
                dateval[4] = dateval[4] - minuteoffset
                dateval[8] = 0  # assert that DST is absent.
                timestamp = time.mktime(dateval)
            versionmap[revision] = friendly
            entry = HistoryEntry(revision, friendly, timestamp)
            entries.append(entry)

        entries.sort(reverse = True)
        nextentry = None
        linkedentries = []
        for entry in entries:
            if nextentry and nextentry.version_id == entry.version_id:
                pass
            else:
                if nextentry:
                    entry.next = nextentry
                    nextentry.previous = entry
                nextentry = entry
                linkedentries.append(entry)
        return (versionmap, linkedentries)

    def old_readstream(self, key, version):
        return self.pipe('cat -r ' + version + ' ' + shell_quote(key)) or None

    def diff(self, key, v1, v2):
        return Diff.Diff(key, v1, v2,
                         self.pipe_lines('diff -r %s -r %s %s' % \
                                         (v1, v2, shell_quote(key)),
                                         True))

    def process_transaction(self, changed, deleted):
        actually_deleted = FileStore.process_transaction(self, changed, deleted)
        cmd = '( cd ' + self.dirname + ' && ('
        touched = ''
        if actually_deleted:
            keys = quote_keys(actually_deleted)
            cmd = cmd + ' hg remove ' + keys + ' ;'
            touched = touched + ' ' + keys
        if changed:
            keys = quote_keys(changed)
            cmd = cmd + ' hg add ' + keys + ' ;'
            touched = touched + ' ' + keys
        if touched:
            cmd = cmd + ' hg commit --user=' + shell_quote(self.author_email) + \
                  ' -m ' + \
                  shell_quote(describe_transaction(changed, actually_deleted)) + \
                  touched
            cmd = cmd + ' )) >/dev/null 2>&1'
            self.system(cmd, 'process_transaction')
        return actually_deleted

class StringAccumulator(StringIO.StringIO):
    def __init__(self):
        StringIO.StringIO.__init__(self)

    def close(self):
        x = self.buf
        StringIO.StringIO.close(self)
        self.buf = x

# Really only a pseudo-transaction, as it doesn't provide ACID
class Transaction(Store):
    def __init__(self, backing):
        self.message_encoder_class = backing.message_encoder_class
        Store.__init__(self)
        self.backing = backing
        self.reset()

    def reset(self):
        self.changed = {}
        self.deleted = sets.Set()

    def keys(self):
        result = self.backing.keys()
        result.extend(k for k in self.changed.keys())
        return result

    def keys_glob(self, keyglob):
        result = self.backing.keys_glob(keyglob)
        result.extend(k for k in self.changed.keys() if fnmatch.fnmatch(k, keyglob))
        return result

    def has_key(self, key):
        return (self.backing.has_key(key) and key not in self.deleted) or key in self.changed

    def gethistory(self, key):
        return self.backing.gethistory(key)

    def getreadstream(self, key, version):
        if version is None:
            if key in self.changed:
                return StringIO.StringIO(self.changed[key].getvalue())
            elif key in self.deleted:
                return None
            else:
                pass
        return self.backing.getreadstream(key, version)

    def getwritestream(self, key):
        f = StringAccumulator()
        self.deleted.discard(key)
        self.changed[key] = f
        return f

    def delete(self, key):
        self.changed.pop(key, None)
        self.deleted.add(key)

    def diff(self, key, v1, v2):
        return self.backing.diff(key, v1, v2)

    def commit(self):
        changedvalues = {}
        # for key in self.changed:
        #     changedvalues[key] = self.changed[key].getvalue()
        # self.backing.process_transaction(changedvalues, self.deleted)
        self.reset()

class Item:
    # Subclasses must provide self.default_properties, a dict

    def __init__(self, store, key, version):
        self.msgenc = store.message_encoder()
        self.key = key
        self.version = version
        self._body = None
        self.primitive_load()

    def body(self):
        if self._body is None:
            self._body = self.msgenc.getbody(self.key, self.version)
        return self._body

    def exists(self):
        return self.msgenc.has_key(self.key)

    def primitive_load(self):
        properties = self.msgenc.getheaders(self.key, self.version)
        defaults = self.default_properties
        for k in defaults:
            defval = defaults[k]
            if isinstance(defval, types.FunctionType) and not properties.has_key(k):
                setattr(self, k, defval(self))
            else:
                setattr(self, k, properties.get(k, defval))

    def primitive_save(self):
        defaults = self.default_properties
        properties = {}
        for k in defaults:
            properties[k] = getattr(self, k)
        self.msgenc.setmessage(self.key, properties, self.body())

    def primitive_delete(self):
        self.msgenc.delete(self.key)

    def history(self):
        import Config
        entries = self.msgenc.gethistory(self.key)
        for entry in entries:
            props = self.msgenc.getheaders(self.key, entry.version_id)
            entry.author = props.get('author', '') or Config.anonymous_user
        return entries

    def head_version(self):
        entries = self.msgenc.gethistory(self.key)
        if entries:
            return entries[0].version_id
        else:
            return None

    def friendly_version(self):
        if self.version:
            e = self.msgenc.gethistoryentry(self.key, self.version)
            if e: return e.friendly_id
        return self.version
