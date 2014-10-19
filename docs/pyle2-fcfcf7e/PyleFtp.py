#!/usr/bin/env python2.5
# -*- python -*-
import sys
import posix
import FtpServer
import Config
import User
import Core
import Store
import web

class PyleFtpFS(FtpServer.FlatFileSystem):
	def __init__(self, *args):
		FtpServer.FlatFileSystem.__init__(self, *args)
		self.file_store = Store.Transaction(Config.file_store)
		self.cache = Store.Transaction(Config.cache_store)
		web.load()
		web.ctx.store = self.file_store
		web.ctx.cache = self.cache
		web.ctx.attachments = Store.Transaction(Config.attachment_store)
		web.ctx.printmode = False
		web.ctx.home = Config.canonical_base_url

	def commit_transaction(self):
		self.file_store.commit()
		self.cache.commit()

	def log_action(self, action, argl):
		import time

		timestamp = time.strftime('%Y/%m/%d %H:%M:%S', time.localtime(time.time()))
		entry = '\t'.join([timestamp, self.user.username, action, str(argl)])
		print entry
		sys.stdout.flush()

	def login_check(self, username, password):
		self.user = User.lookup(username)
		if not Config.user_authenticator.authenticate(self.user, password):
			return 0
		self.log_action('login', [])
		return 1

	def allFilenames(self):
		keys = self.file_store.message_encoder().keys_glob('*.txt')
		keys.sort()
		return [k[:-4] for k in keys]

	def contains(self, filename):
		return self.file_store.message_encoder().has_key(filename + '.txt')

	def _retrieve_page(self, filename, must_exist = True):
		if must_exist and not self.contains(filename):
			raise FtpResponse(550, 'File not found')
		return Core.Page(filename)

	def contentsFor(self, filename):
		page = self._retrieve_page(filename)
		if not page.readable_for(self.user):
			raise FtpResponse(550, 'Read permission denied')
		return page.body()

	def lengthOf(self, filename):
		page = self._retrieve_page(filename)
		return len(page.body())

	def userFor(self, filename):
		page = self._retrieve_page(filename)
		return page.author or Config.anonymous_user

	def mtimeFor(self, filename):
		page = self._retrieve_page(filename)
		return page.timestamp_epoch()

	def updateCheck(self, filename):
		if filename.endswith('~'):
			# Don't let Emacs save "backups" here.
			return 0
		page = self._retrieve_page(filename, False)
		return page.writable_for(self.user)

	def updateContent(self, filename, newcontent):
		page = self._retrieve_page(filename, False)
		page.setbody(newcontent)
		page.save(self.user, None)
		self.commit_transaction()
		self.log_action('stor', [filename])

	def deleteContent(self, filename):
		page = self._retrieve_page(filename)
		if not page.writable_for(self.user):
			raise FtpResponse(550, 'Delete permission denied')
		page.delete(self.user)
		self.commit_transaction()
		self.log_action('dele', [filename])

if __name__ == '__main__':
	def arg(n, d):
		if len(sys.argv) > n:
			return sys.argv[n]
		else:
			return d
	hostname = arg(1, '127.0.0.1')
	portnumber = int(arg(2, '8021'))
	
	Core.init_pyle()
	print 'Listening on %s:%d' % (hostname, portnumber)
	svr = FtpServer.FtpServer((hostname, portnumber), PyleFtpFS,
				  bannerText = 'Pyle FTP service ready.')

	if posix.getuid() == 0:
		print "Running as root, suing to " + Config.ftp_server_user
		import pwd
		try:
			posix.setuid(pwd.getpwnam(Config.ftp_server_user)[2])
		except KeyError:
			print "Cannot su to " + Config.ftp_server_user + "!"

	svr.serve_forever()
