import os.path as path
import re
from binascii import hexlify as hx, unhexlify as uhx
from pathlib import Path
my_file = Path('keys.txt')
my_file2 = Path('ztools\\keys.txt')	

def _normalize_key_name(keyname):
	"""Normalize master_key and titlekek names from hex to decimal two-digit format.
	E.g. master_key_0a -> master_key_10, titlekek_0b -> titlekek_11
	"""
	for prefix in ('master_key_', 'titlekek_', 'key_area_key_application_', 
	               'key_area_key_ocean_', 'key_area_key_system_'):
		if keyname.startswith(prefix):
			suffix = keyname[len(prefix):]
			# Only process numeric/hex suffixes
			try:
				num = int(suffix, 16)
				return '{}{:02d}'.format(prefix, num)
			except ValueError:
				pass
	return keyname

class Keys(dict):
	def __init__(self, keys_type):
		self.keys_type = keys_type
		is_key  = re.compile(r'''\s*([a-zA-Z0-9_]*)\s* # name
								=
								\s*([a-fA-F0-9]*)\s* # key''', re.X)
		f = None
		try:
			if my_file.is_file():
				f = open('keys.txt', 'r')
			elif my_file2.is_file():
				f = open('ztools\\keys.txt', 'r')
		except FileNotFoundError:
			pass
		
		if f is None:
			try:
				f = open(path.join(path.dirname(path.abspath(__file__)), '%s' % self.keys_type), 'r')
			except FileNotFoundError:
				raise FileNotFoundError('Need key file %s.keys in either %s or %s' % (self.keys_type, 
					path.expanduser('~/.switch'), path.dirname(path.abspath(__file__))))
		iterator = (re.search(is_key, l) for l in f)
		raw_dict = {}
		for r in iterator:
			if r is not None:
				keyname = r[1]
				# Normalize key names from hex indexing to decimal
				keyname = _normalize_key_name(keyname)
				raw_dict[keyname] = uhx(r[2])
		super(Keys, self).__init__(raw_dict)
		f.close()

	def __getitem__(self, item):
		try:
			return dict.__getitem__(self, item)
		except KeyError:
			# Try alternate naming: if requesting decimal, try hex, and vice versa
			# e.g. master_key_10 might be stored as master_key_0a
			raise KeyError('Missing key %s in %s' % (item, self.keys_type))

class ProdKeys(Keys):
	def __init__(self):
		super(ProdKeys, self).__init__('keys.txt')
		if 'header_key' in self:
			self['nca_header_key'] = self.pop('header_key')

class DevKeys(Keys):
	def __init__(self):
		super(DevKeys, self).__init__('dev')

class TitleKeys(Keys):
	def __init__(self):
		super(TitleKeys, self).__init__('title')
		if 'header_key' in self:
			self['nca_header_key'] = self.pop('header_key')
