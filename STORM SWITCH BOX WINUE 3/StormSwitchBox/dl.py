import urllib.request
data = urllib.request.urlopen('https://raw.githubusercontent.com/nicoboss/nsz/master/nsz/SolidDecompressor.py').read().decode('utf-8')
print(data[:3000])
