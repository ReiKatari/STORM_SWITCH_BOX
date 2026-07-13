import re
import aes128
from binascii import hexlify as hx, unhexlify as uhx
import Print
from pathlib import Path
import sq_settings

keys = {}
titleKeks = []
keyAreaKeys = []

def getMasterKeyIndex(i):
	if i > 0:
		return i-1
	else:
		return 0

def keyAreaKey(cryptoType, i):
	# print(cryptoType)
	# print(i)
	return keyAreaKeys[cryptoType][i]

def get(key):
	return keys[key]
	
def getTitleKek(i):
	return titleKeks[i]
	
def decryptTitleKey(key, i):
	kek = getTitleKek(i)
	
	crypto = aes128.AESECB(uhx(kek))
	return crypto.decrypt(key)
	
def encryptTitleKey(key, i):
	kek = getTitleKek(i)
	
	crypto = aes128.AESECB(uhx(kek))
	return crypto.encrypt(key)
	
def changeTitleKeyMasterKey(key, currentMasterKeyIndex, newMasterKeyIndex):
	return encryptTitleKey(decryptTitleKey(key, currentMasterKeyIndex), newMasterKeyIndex)

def generateKek(src, masterKey, kek_seed, key_seed):
	kek = []
	src_kek = []

	crypto = aes128.AESECB(masterKey)
	kek = crypto.decrypt(kek_seed)

	crypto = aes128.AESECB(kek)
	src_kek = crypto.decrypt(src)

	if key_seed != None:
		crypto = aes128.AESECB(src_kek)
		return crypto.decrypt(key_seed)
	else:
		return src_kek

def _normalize_master_key_name(keyname):
	"""Normalize master_key names to consistent decimal two-digit format.
	Handles both hex-indexed keys (0a, 0b, ..., 0f, 10, 11, ..., 15)
	and decimal-indexed keys (00, 01, ..., 09, 10, 11, ...).
	Returns normalized keyname like 'master_key_00', 'master_key_10', etc.
	"""
	suffix = keyname[len('master_key_'):]
	# Try parsing as hex first (handles 0a, 0b, 0c, 0d, 0e, 0f)
	try:
		num = int(suffix, 16)
	except ValueError:
		try:
			num = int(suffix, 10)
		except ValueError:
			return keyname
	return 'master_key_{:02d}'.format(num)

def unwrapAesWrappedTitlekey(wrappedKey, keyGeneration):
	aes_kek_generation_source = uhx(keys['aes_kek_generation_source'])
	aes_key_generation_source = uhx(keys['aes_key_generation_source'])
	
	mk = 'master_key_{:02d}'.format(keyGeneration)

	kek = generateKek(uhx(keys['key_area_key_application_source']), uhx(keys[mk]), aes_kek_generation_source, aes_key_generation_source)

	crypto = aes128.AESECB(kek)
	return crypto.decrypt(wrappedKey)		
	
def getKey(key):
	if key not in keys:
		raise IOError('%s missing from keys.txt' % key)
	return uhx(keys[key])

def masterKey(masterKeyIndex):
	return getKey('master_key_{:02d}'.format(masterKeyIndex))

def load(fileName):
	global keyAreaKeys
	global titleKeks

	with open(fileName, encoding="utf8") as f:
		for line in f.readlines():
			r = re.match('\s*([a-z0-9_]+)\s*=\s*([A-F0-9]+)\s*', line, re.I)
			if r:
				keyname=r.group(1)
				if keyname.startswith('master_key_') and keyname != 'master_key_source':
					keyname = _normalize_master_key_name(keyname)
				keys[keyname] = r.group(2)				
		# for k in keys.keys():
			# print(k)
	
	#crypto = aes128.AESCTR(uhx(key), uhx('00000000000000000000000000000010'))
	aes_kek_generation_source = uhx(keys['aes_kek_generation_source'])
	aes_key_generation_source = uhx(keys['aes_key_generation_source'])

	keyAreaKeys = []
	for i in range(40):
		keyAreaKeys.append([None, None, None])

	
	for i in range(40):
		masterKeyName = 'master_key_{:02d}'.format(i)
		if masterKeyName in keys.keys():
			# aes_decrypt(master_ctx, &keyset->titlekeks[i], keyset->titlekek_source, 0x10);
			masterKey = uhx(keys[masterKeyName])
			crypto = aes128.AESECB(masterKey)
			titleKeks.append(crypto.decrypt(uhx(keys['titlekek_source'])).hex())
			keyAreaKeys[i][0] = generateKek(uhx(keys['key_area_key_application_source']), masterKey, aes_kek_generation_source, aes_key_generation_source)
			keyAreaKeys[i][1] = generateKek(uhx(keys['key_area_key_ocean_source']), masterKey, aes_kek_generation_source, aes_key_generation_source)
			keyAreaKeys[i][2] = generateKek(uhx(keys['key_area_key_system_source']), masterKey, aes_kek_generation_source, aes_key_generation_source)
		else:
			pass

if sq_settings.key_system =="production":
	raw_keys_file = Path('keys.txt')
	raw_keys_file2 = Path('ztools\\keys.txt')
	raw_keys_file3 = Path('ztools/keys.txt')
else:
	raw_keys_file = Path('dev_keys.txt')
	raw_keys_file2 = Path('ztools\\dev_keys.txt')
	raw_keys_file3 = Path('ztools/keys.txt')	
	
if raw_keys_file.is_file():
	load(raw_keys_file)
elif raw_keys_file2.is_file():
	load(raw_keys_file2)
elif raw_keys_file3.is_file():
	load(raw_keys_file3)	
	
if not raw_keys_file.is_file() and not raw_keys_file2.is_file() and not raw_keys_file3.is_file():
	print('keys.txt missing')
		
#for k in titleKeks:
#	Print.info('titleKek = ' + k)

#for k in keyAreaKeys:
#	Print.info('%s, %s, %s' % (hex(k[0]), hex(k[1]), hex(k[2])))