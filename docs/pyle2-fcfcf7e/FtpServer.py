#
# FtpServer.py - Implementation of a (mostly) RFC-959-compatible server.
# Copyright (C) 2001 Tony Garnock-Jones <tonyg@kcbbs.gen.nz>
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
#

"""Generic FTP server class. FTP is RFC 959.

Classes:
- FtpServer: inherits ThreadingTCPServer. Uses FtpSession.
- FtpSession: a SocketServer.StreamRequestHandler which implements the FTP protocol.
- FtpFileSystem: an abstract file system to be accessed via FtpServer.

To use this module, you must provide an implementation of
FtpFileSystem to the constructor for FtpServer. See the example near
the end of the file (which will be run if this is the main module).

There's a convenience class, FlatFileSystem, which provides a simple
implementation of FtpFileSystem and delegates the specifics of any
given flat file system to its subclasses. An example of FlatFileSystem
in use is DictFs.

For example:
	import FtpServer
		
	class MyFS(FtpFileSystem):
		...

	# Creates a server on the 'localhost' interface, at
	# port 8021, where each FtpSession will access a
	# file system implemented by MyFS:
		
	myServer = FtpServer.FtpServer(('localhost', 8021), MyFS)
	myServer.serve_forever()
	
"""

__version__ = '0.1'


import os
import sys
import string
import socket
import SocketServer
import time

#---------------------------------------------------------------------------
# Helper classes and functions

class FtpResponse:
	"""Class representing a response sent from the FTP server to
	the FTP client.

	Instance variables:
		- code		Numeric response code. See the RFC.
		- message	Human-readable message. Some responses
				require a fixed format for these, too.
				Again, see the RFC for details.
	"""
	
	def __init__(self, code, message):
		self.code = code
		self.message = message

	def render(self):
		return str(self.code) + ' ' + self.message


def subClassResponsibility(what):
	"""Used in a few places to indicate an unimplemented method
	that should have been implemented in a subclass."""
	
	raise FtpResponse(502, "Unimplemented subClassResponsibility: " + str(what))


class FtpError:
	"""Represents an exception to be thrown during server
	operation."""

	def __init__(self, message):
		self.message = message

	def render(self):
		return self.message


def ftp_quote(s):
	"""Implements the quote-doubling escape technique required by
	the RFC."""
	return '"' + string.replace(s, '"', '""') + '"'


#---------------------------------------------------------------------------
# Main Classes

class FtpFileSystem:

	"""Base class - subclasses implement a file system to be
	served by FtpServer/FtpSession. Note that there is one
	instance of FtpFileSystem for *each* active FtpSession being
	served! (So if you want each session to see the same picture
	of reality, you have to use a thread-safe class variable or
	somesuch.)

	Instance variables:
		- session	The FtpSession object managing this FileSystem

	To be implemented in subclasses:
		- login_check
		- getcwd
		- cwd
		- cdup
		- list
		- retrieve
		- store_check
		- store
		- delete
	"""
	
	def __init__(self, session, username, password):

		"""Pass in an instance of FtpSession, along with a
		username/password that is to be checked. Will raise an
		FtpResponse if the login_check doesn't work
		out. Otherwise, keeps hold of the FtpSession object as
		self.session."""
		
		self.session = session
		
		if not self.login_check(username, password):
			raise FtpResponse(530, 'Login incorrect.')

	def login_check(self, username, password):

		"""To be implemented in subclasses - should check that
		the given username has access to this FtpFileSystem,
		and that the password is correct. Should return a true
		value if everything went okay, or a false value to
		indicate that a login-incorrect message should be sent
		back to the client from the server. May raise
		FtpResponse, but note that the specific case of
		530-login-incorrect is dealt with by our caller."""

		subClassResponsibility("FtpFileSystem.login_check")

	def getcwd(self):

		"""To be implemented in subclasses - should return a
		string that is the name of the current working
		directory, in format appropriate for this
		FtpFileSystem's implementation. May raise
		FtpResponse."""

		subClassResponsibility("FtpFileSystem.getcwd")

	def cwd(self, newpath):

		"""To be implemented in subclasses - should alter the
		current working directory according to the 'newpath'
		parameter. The format of 'newpath' is
		filesystem-specific. May raise FtpResponse."""

		subClassResponsibility("FtpFileSystem.cwd")

	def cdup(self):

		"""To be implemented in subclasses - should move the
		current working directory to be its parent directory.
		May raise FtpResponse."""

		subClassResponsibility("FtpFileSystem.cdup")

	def list(self, pathname):

		"""To be implemented in subclasses - should return an
		ASCII listing of the contents of the current working
		directory, in a filesystem-specific format. Note that
		this means that the line-separator should be CRLF, not
		simply CR or LF. May raise FtpResponse."""

		subClassResponsibility("FtpFileSystem.list")

	def retrieve(self, pathname):

		"""To be implemented in subclasses - should return the
		contents of the given path (which may be absolute or
		cwd-relative) as a string. May raise FtpResponse."""

		raise FtpResponse(550, 'File ' + pathname + ' not found')

	def store_check(self, pathname):

		"""To be implemented in subclasses. This method is
		called before attempting to process an upload
		request. It should raise FtpResponse if the upload
		should be denied, and should just return as per normal
		if the upload should go ahead. The upload will then
		proceed, followed by a call to store() below."""

		raise FtpResponse(550, 'File ' + pathname + ' cannot be stored')

	def store(self, pathname, contents):

		"""To be implemented in subclasses. This method is
		called after store_check() above has already
		authorised the upload. It should put the file contents
		given in the string 'contents' into the file named by
		'pathname'. May raise FtpResponse, but the upload has
		already happened by the time this method is called."""

		raise FtpResponse(426, 'File ' + pathname + ' cannot be stored')

	def delete(self, pathname):

		"""To be implemented in subclasses - should
		unlink/delete the file referred to by pathname. May
		raise FtpResponse."""

		subClassResponsibility("FtpFileSystem.delete")

class FtpSession(SocketServer.StreamRequestHandler):

	"""Implements the actual FTP protocol given in RFC 959.
	Mostly, at any rate. There are some incompatibilities and
	unimplemented bits.

	Instance variables:
	- stillrunning: set to false to cause the session to quit cleanly
	- logged_in: true if we have been successfully logged in to.
	- dataconn: a file-stream-over-socket for transport of file data
	- passiveserver: None, or a server socket for passive connections
	- fs: a per-FtpSession instance of FtpFileSystem. Created at login.

	- command, argument: the most recently read FTP command and
          its argument, if any.

	- ipaddr, portnum: specification for the next active
          connection to open. See handle_PORT(), openDataConn().

	See mainloop() for the spine of the code.
	"""

	def handle(self):
		"""This is the 'main body' of a StreamRequestHandler."""

		# First, display our banner.
		self.respond(FtpResponse(220, self.server.banner))

		self.stillrunning = 1
		
		self.logged_in = None

		self.passiveserver = None
		self.dataconn = None
		self.ipaddr = None
		self.portnum = None
		
		self.fs = None
		
		try:
			try:
				self.mainloop()
			except FtpError, e:
				print "Error: " + e.render()
				self.respond(FtpResponse(421, e.render()))
		finally:
			self.cleanup()

	def cleanup(self):
		"""Performs a clean shutdown of the session."""
		self.closeDataConn()

	def closeDataConn(self):
		"""Closes our data connection(s), if any."""
		
		if self.dataconn:
			self.dataconn.close()
			self.dataconn = None

		if self.passiveserver:
			self.passiveserver.close()
			self.passiveserver = None

		self.ipaddr = None
		self.portnum = None

	def getcommand(self):
		"""Reads an FTP command from our control connection."""

		self.input_line = string.strip(self.rfile.readline())
		if not self.input_line:
			raise FtpError("End of input reached")

		elts = string.split(self.input_line, ' ', 1)
		if len(elts) < 2:
			# Cope with a command sans argument.
			elts.append('')

		(self.command, self.argument) = elts

		self.command = string.upper(self.command)
		#print "Got command: " + self.command + ' -- ' + self.argument

	def respond(self, resp):
		"""Renders an FtpResponse onto our control connection
		in response to some event."""
		
		self.wfile.write(resp.render() + '\r\n')

	def checkAuth(self):
		"""Called in circumstances where we *must* have
		successfully authenticated a user. Will raise
		FtpResponse 530 if a login needs to happen."""
		
		if not self.logged_in:
			raise FtpResponse(530, 'Please login with USER and PASS.')

	def mainloop(self):
		"""Runs the body of the session. Repeatedly reads a
		command and processes it."""
		
		while self.stillrunning:
			try:
				self.getcommand()

				# These commands are the only ones that work when
				# we're not in "logged in" state:
				if not self.command in ['USER', 'PASS', 'QUIT']:
					self.checkAuth()

				# Each read command is handled by a
				# method name constructed and called
				# via introspection:
				#   "handle_COMMAND(self, argument)"
				
				cmd_key = 'handle_' + self.command

				if hasattr(FtpSession, cmd_key):
					# Hooray, we have a handler for that command.
					r = getattr(FtpSession, cmd_key)(self, self.argument)
				else:
					# Hmm, we don't appear to implement that command.
					r = FtpResponse(500, "'" + self.command + ' ' \
								+ self.argument \
								+ "': command not understood.")

				# Command handlers are expected to
				# either raise an FtpResponse if
				# something went wrong, or return an
				# instance of FtpResponse to return to
				# the client as a normal response.
				
				self.respond(r)

			except FtpResponse, rerr:

				# Here's where the command handler has
				# raised an instance of FtpResponse,
				# ie. an "error" response.
				
				self.respond(rerr)

	def preparePassiveConn(self):

		"""Prepares a passive socket server on a free
		port. Raises FtpError if any of the socket operations
		fail. Returns the address of the newly-created socket
		server."""
		
		try:
			svr = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
			svr.bind((self.server.hostname, 0))
			svr.listen(1)
		except socket.error, e:
			raise FtpError("couldn't PASV: " + str(e))

		self.passiveserver = svr

		addr = list(svr.getsockname())
		if addr[0] == '0.0.0.0':
			# INADDR_ANY. Replace this with a real IP number.
			if self.server.canonicalIP:
				# We've been told which IP to use.
				addr[0] = self.server.canonicalIP
			else:
				# We haven't been told - so guess at our canonical hostname.
				addr[0] = socket.gethostbyname(socket.gethostname())

		return addr

	def openDataConn(self,mode):

		"""Opens a data connection socket to the correct
		address and port, in the specified direction (mode may
		be 'wb' or 'rb' for outbound or inbound connections,
		respectively)."""

		if self.passiveserver:
			# Passive mode. Accept one connection.
			s, addr = self.passiveserver.accept()
			self.passiveserver.close()
			self.passiveserver = None
		else:
			# Active mode. Create one connection.
			s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
			s.connect((self.ipaddr, self.portnum))

		self.dataconn = s.makefile(mode)

	def handle_USER(self, username):

		"""Process a login attempt. Contains an inner loop in
		which we wait for a PASS command. Constructs in
		instance of our FtpServer's fs_class instance variable
		(self.server is populated by SocketServer) to perform
		authentication and process further requests. %%%bug:
		should accept QUIT in the inner loop?"""
		
		self.respond(FtpResponse(331, 'Password required for ' + username + '.'))
		while 1:
			self.getcommand()
			if self.command != 'PASS':
				self.respond(FtpResponse(530, 'Please login with USER and PASS.'))
				continue
			break

		password = self.argument

		# Here's where the authentication actually takes place.
		
		self.fs = self.server.fs_class(self, username, password)

		# Now we're logged in okay.
		
		self.logged_in = username
		return FtpResponse(230, 'User ' + username + ' logged in.')

	def handle_PASS(self, dummy):
		"""Handles a PASS command without a corresponding USER
		command."""
		
		return FtpResponse(503, 'Login with USER first.')

	def handle_QUIT(self, dummy):
		"""Handles a QUIT command. Could perhaps summarise the
		session here?"""
		
		self.stillrunning = 0
		return FtpResponse(221, 'Goodbye.')

	def handle_TYPE(self, typespec):

		"""Handles a TYPE command (poorly). Here's one of the
		areas in which we depart from the RFC
		specification. We kinda treat ASCII and binary as
		being one-and-the-same thing... this will probably
		cause problems on Macs etc. %%%"""
		
		typespec = string.split(string.upper(typespec))

		if typespec[0] == 'I':
			return FtpResponse(200, 'Sure, binary, why not.')

		if typespec[0] == 'A':
			if (len(typespec) > 1) and typespec[1] != 'N':
				return FtpResponse(502, 'Unimplemented ASCII subTYPE.')
			return FtpResponse(200, 'Type set to ' + typespec[0] + '.')

		return FtpResponse(502, 'Unimplemented TYPE.')

	def handle_MODE(self, modespec):

		"""Yeah, here's another poorly implemented area. The
		spec mandates that we support MODE to be 'minimally
		compliant', but we don't really do anything useful
		here. %%%"""
		
		if modespec != 'S':
			return FtpResponse(502, 'Unimplemented transfer MODE.')

		return FtpResponse(200, 'Mode set to ' + modespec + '.')

	def handle_STRU(self, structurespec):

		"""And this is a third poorly implemented area. See
		the RFC. %%%"""
		
		if structurespec != 'F':
			return FtpResponse(502, \
				'Unimplemented file STRUcture. Not RFC compatible!')

		return FtpResponse(200, 'File structure set to ' + structurespec + '.')

	def handle_SYST(self, dummy):

		"""Again, we fudge things a bit, this time not because
		it's a pain to implement, but because I couldn't be
		bothered looking for the correct response to give
		here. Lazy! %%% Note: not RFC compliant here!"""
		
		return FtpResponse(215, 'UNIX Type: Pyle FTP server')

	def handle_CWD(self, newdir):
		"""Handle a CWD command by delegating to our FtpFileSystem."""
		
		return self.fs.cwd(newdir)

	def handle_CDUP(self, dummy):
		"""Handle a CDUP command by delegating to our FtpFileSystem."""
		
		return self.fs.cdup()

	def handle_PWD(self, dummy):
		"""Handle a PWD command by (partly) delegating to our
		FtpFileSystem."""
		
		return FtpResponse(257, ftp_quote(self.fs.getcwd()) + ' is current directory.')

	def handle_PORT(self, spec):

		"""Handle an active-port-setting command. Store ipaddr
		and portnum for later use. (See openDataConn
		above.)"""

		self.closeDataConn()
		
		parts = string.split(spec, ',')
		self.ipaddr = string.join(parts[0:4], '.')
		self.portnum = int(parts[4]) * 256 + int(parts[5])
		
		return FtpResponse(200, 'PORT successful; will use IP ' + self.ipaddr + \
					' and port ' + str(self.portnum))

	def handle_PASV(self, dummy):

		"""Handle a PASV passive-connection request. (See
		preparePassiveConn and openDataConn above.)"""

		self.closeDataConn()

		addr = self.preparePassiveConn()
		
		bytes = string.split(addr[0], '.')
		bytes.append(addr[1] / 256)
		bytes.append(addr[1] % 256)
		bytes = map(str, bytes)
		
		return FtpResponse(227, "Entering Passive Mode (" + string.join(bytes, ',') + ").")

	def handle_RETR(self, pathname):

		"""Handle a RETR command, a request to download a
		file. First delegate to our FtpFileSystem to get the
		content to send, then open a connection and send
		it."""
		
		contents = self.fs.retrieve(pathname)
		self.openDataConn('wb')

		# Note how we callously ignore the TYPE that was set earlier!
		self.respond(FtpResponse(150, 'Opening BINARY mode connection for ' \
						+ pathname + ' (' + str(len(contents)) + ').'))
		
		self.dataconn.write(contents)
		self.closeDataConn()
		
		return FtpResponse(226, 'Transfer complete.')

	def handle_STOR(self, pathname):

		"""Handle a STOR command, a request to upload a
		file. First delegate to our FtpFileSystem to check
		that the upload is allowed (it should raise
		FtpResponse if it isn't), then open a connection and
		read the data to store. Finally, pass that data on to
		our FtpFileSystem."""
		
		self.fs.store_check(pathname)
		self.openDataConn('rb')

		# Note how we callously ignore the TYPE that was set earlier!
		self.respond(FtpResponse(150, 'Opening BINARY mode connection for ' \
						+ pathname + '.'))
		
		contents = self.dataconn.read()
		self.closeDataConn()
		
		self.fs.store(pathname, contents)
		
		return FtpResponse(226, 'Transfer complete.')

	def handle_DELE(self, pathname):

		"""Handle a DELE command, a request to remove a file
		from the server. We delegate to our FtpFileSystem,
		which should raise FtpResponse if the request was
		unsuccessful."""
		
		self.fs.delete(pathname)
		return FtpResponse(250, 'DELE command successful.')

	def handle_NLST(self, pathname):
		"""Handle an NLST request. Delegate (not really
		correctly) to LIST."""

		return self.handle_LIST(pathname)

	def handle_LIST(self, pathname):

		"""Handle a LIST command, a request for a listing of
		the current-working-directory on the server. We
		delegate to our FtpFileSystem to get the listing, and
		then open a data connection and transmit the string we
		constructed."""
		
		listing = self.fs.list(pathname)
		#print "*** Sending listing:"
		#print listing
		
		self.openDataConn('wb')
		self.respond(FtpResponse(150, 'Opening ASCII mode data connection for listing.'))
		self.dataconn.write(listing)
		self.closeDataConn()
		
		return FtpResponse(226, 'Transfer complete.')

	def handle_NOOP(self, dummy):
		"""Handle the NOOP command. It's in the RFC!"""
		return FtpResponse(200, 'Noop command successful.')


class FtpServer(SocketServer.ThreadingTCPServer):

	"""Main entry point for the FtpServer module - construct an
	instance of this class, passing in a subclass of FtpFileSystem
	(the actual class itself, not an instance!), and call
	serve_forever on it. See the example in the module's
	docstring."""

	def __init__(self, server_address, fsClass, bannerText = None, canonicalIP = None):

		"""Construct an FtpServer.
		- server_address: tuple of the form (hostname, portnumber)
		- fsClass: a class. Must inherit from FtpFileSystem.
		- bannerText: optional welcome message to use.
		- canonicalIP: optional string 'xxx.yyy.zzz.www' containing IP to use
		    in passive connects.
		"""

		SocketServer.ThreadingTCPServer.__init__(self, server_address, FtpSession)

		self.hostname = server_address[0]
		self.fs_class = fsClass
		
		if bannerText:
			self.banner = bannerText
		else:
			self.banner = 'Python FtpServer.py (Version ' + __version__ + ') ready.'

		self.canonicalIP = canonicalIP

	def server_bind(self):

		"""We extend server_bind here from ThreadingTCPServer
		to set the SO_REUSEADDR flag, so that we don't have to
		wait through the stand-down period before restarting
		our server."""
		
		self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
		SocketServer.ThreadingTCPServer.server_bind(self)


#---------------------------------------------------------------------------
# Convenience classes

def stripslash(k):
	if not k:
		return ''
	elif k[0] == '/':
		return stripslash(k[1:])
	else:
		return k
	if string.find(k, '/') != -1:
		raise FtpResponse(501, 'Filename may not contain slash (/)')

def formatentry_ls(l):

	"""Returns a string in a format similar to that produced by
	'ls -la'. This is particularly handy to get Emacs' ange-ftp
	working well with FtpServers.

	Expects a list or tuple containing the following members:
		- [0] - permissions, in 'drwxrwxrwx' format
		- [1] - number of links (doesn't really matter much)
		- [2] - file's owner's username (eg. 'root')
		- [3] - file's group's groupname (eg. 'wheel')
		- [4] - file's length in bytes
		- [5] - file's mtime, in seconds since epoch
		- [6] - file's name
	"""
	
	mtime = l[5]
	if time.time() - mtime > (86400 * 28):
		printtime = time.strftime('%b %d %Y', time.localtime(mtime))
	else:
		printtime = time.strftime('%b %d %H:%M', time.localtime(mtime))
	l[5] = printtime
	return '%s %3d %-8s %-8s %8d %-12s %s' % tuple(l)

class FlatFileSystem(FtpFileSystem):

	"""An implementation of FtpFileSystem that provides a simple
	flat file system. The actual mechanics of the file system
	backend are still left to subclasses - this class essentially
	implements all the protocol to do with directories, leaving
	subclasses able to concentrate on storage.

	Subclasses should implement:
		- allFilenames
		- contains
		- contentsFor
		- lengthOf
		- userFor (optional)
		- mtimeFor (optional)
		- updateCheck (optional)
		- updateContent
		- deleteContent

	"""
	
	def getcwd(self):
		"""Implements FtpFileSystem.getcwd."""
		return '/'

	def cwd(self, newpath):
		"""Implements FtpFileSystem.cwd."""
		if newpath == '/' or newpath == '/.':
			return FtpResponse(250, 'CWD command successful.')
		else:
			return FtpResponse(550, newpath + ': No such file or directory.')

	def listing_for(self, k):

		"""Returns an appropriate entry for use in the
		implementation of the list() method. Special-cases the
		filename '.' as the contents of our (single)
		directory."""
		
		if k == '.' or k == '':
			result = 'total 0\r\n' \
				+ formatentry_ls(['drwx------',
						  1,
						  'unknown',
						  'unknown',
						  0,
						  0,
						  '.']) \
				+ '\r\n' \
				+ formatentry_ls(['drwx------',
						  1,
						  'unknown',
						  'unknown',
						  0,
						  0,
						  '..']) \
				+ '\r\n'
			for k in self.allFilenames():
				result = result + self.listing_for(k)
			return result
		elif self.contains(k):
			return formatentry_ls(['-rw-------',
					       1,
					       self.userFor(k),
					       self.userFor(k),
					       self.lengthOf(k),
					       self.mtimeFor(k),
					       k]) \
				+ '\r\n'
		else:
			return ''	# this filename isn't present

	def list(self, argstr):
		"""Implements FtpFileSystem.list."""
		
		args = string.split(argstr)
		args = filter(lambda arg: not arg.startswith('-'), args)
		result = ''

		if len(args) == 0:
			# No arguments --> list complete contents
			args = ['/.']

		for k in args:
			k = stripslash(k)
			result = result + self.listing_for(k)

		return result

	def retrieve(self, pathname):
		"""Implements FtpFileSystem.retrieve."""
		
		pathname = stripslash(pathname)
		if self.contains(pathname):
			return self.contentsFor(pathname)
		else:
			raise FtpResponse(550, 'FlatFileSystem file ' + pathname + ' not found')

	def store_check(self, pathname):
		"""Implements FtpFileSystem.store_check."""
		
		pathname = stripslash(pathname)
		if not self.updateCheck(pathname):
			raise FtpResponse(550, 'FlatFileSystem file ' + pathname + ' not storable')

	def store(self, pathname, c):
		"""Implements FtpFileSystem.store."""
		
		pathname = stripslash(pathname)
		self.updateContent(pathname, c)

	def delete(self, pathname):
		"""Implements FtpFileSystem.delete."""
		
		pathname = stripslash(pathname)
		if self.contains(pathname):
			self.deleteContent(pathname)
		else:
			raise FtpResponse(550, 'Path ' + pathname + ' not found in DELE')

	def allFilenames(self):

		"""To be implemented in subclasses - should return a
		list of all filenames in this flat file system. Called
		(indirectly) by FlatFileSystem.list."""
		
		return []

	def contains(self, filename):

		"""To be implemented in subclasses - should return
		true if this FlatFileSystem contains the named file,
		or false otherwise."""
		
		subClassResponsibility("FlatFileSystem.contains")

	def contentsFor(self, filename):

		"""To be implemented in subclasses - should return the
		contents of the named file as a string."""
		
		subClassResponsibility("FlatFileSystem.contentsFor")

	def lengthOf(self, filename):

		"""To be implemented in subclasses - should return the
		size of the named file, in bytes."""
		
		subClassResponsibility("FlatFileSystem.lengthOf")

	def userFor(self, filename):

		"""May be implemented in subclasses - returns the name
		of the user that owns the named file."""
		
		return 'unknown'

	def mtimeFor(self, filename):

		"""May be implemented in subclasses - returns the time
		the named file was last modified, in seconds since the
		epoch (Jan 1, 1970, 00:00:00 UTC)."""
		
		return 0

	def updateCheck(self, filename):

		"""May be implemented in subclasses - returns true if
		a store into the named file is allowed for this
		user."""

		return 1

	def updateContent(self, filename, newcontent):

		"""To be implemented in subclasses - stores some new
		content into a named file."""
		
		subClassResponsibility("FlatFileSystem.updateContent")

	def deleteContent(self, filename):

		"""To be implemented in subclasses - deletes a file
		from this FlatFileSystem."""
		
		subClassResponsibility("FlatFileSystem.deleteContent")


#---------------------------------------------------------------------------
# Simple example/test.

if __name__ == '__main__':
	contents = {}
	content_time = {}

	class DictFs(FlatFileSystem):
		def login_check(self, username, password):
			print "Got user '" + username + "' with password '" + password + "'"
			self.user = username
			return 1

		def allFilenames(self):
			return contents.keys()

		def contains(self, filename):
			return contents.has_key(filename)

		def contentsFor(self, filename):
			return contents[filename]

		def lengthOf(self, filename):
			return len(contents[filename])

		def userFor(self, filename):
			return self.user

		def mtimeFor(self, filename):
			return content_time[filename]

		def updateContent(self, filename, newcontent):
			contents[filename] = newcontent
			content_time[filename] = time.time()

		def deleteContent(self, filename):
			del contents[filename]
			del content_time[filename]

	FtpServer(('localhost', 8021), DictFs).serve_forever()
