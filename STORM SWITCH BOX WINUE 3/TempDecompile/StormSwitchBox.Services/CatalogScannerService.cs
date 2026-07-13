using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ns;
using LibHac.Tools.Es;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Microsoft.UI.Xaml.Media.Imaging;
using StormSwitchBox.Core.NSZ;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class CatalogScannerService
{
	private readonly KeysService _keysService;

	public CatalogScannerService(KeysService keysService)
	{
		_keysService = keysService;
	}

	public async Task ScanDirectoryAsync(string directoryPath, ObservableCollection<CatalogItem> catalog, CancellationToken token)
	{
		if (!Directory.Exists(directoryPath))
		{
			return;
		}
		string[] extensions = new string[4] { ".nsp", ".nsz", ".xci", ".xcz" };
		List<string> files = (from f in Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
			where Enumerable.Contains(extensions, System.IO.Path.GetExtension(f).ToLowerInvariant())
			select f).ToList();
		foreach (string file in files)
		{
			token.ThrowIfCancellationRequested();
			CatalogItem item = new CatalogItem
			{
				FilePath = file,
				FileName = System.IO.Path.GetFileName(file),
				FileSize = ProcessingTask.FormatSize(new FileInfo(file).Length)
			};
			App.MainDispatcher?.TryEnqueue(delegate
			{
				catalog.Add(item);
			});
			await Task.Run(async delegate
			{
				try
				{
					ExtractMetadata(item, file, token);
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					Exception ex3 = ex2;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						item.IsLoading = false;
						item.HasError = true;
						item.ErrorMessage = ex3.Message;
					});
				}
			}, token);
		}
	}

	private void ExtractMetadata(CatalogItem item, string filePath, CancellationToken token)
	{
		bool flag = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);
		using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		IStorage storage = stream.AsStorage();
		IFileSystem fileSystem = null;
		if (flag)
		{
			storage.GetSize(out var size).ThrowIfFailure();
			SubStorage storage2 = new SubStorage(storage, 65536L, size - 65536);
			PartitionFileSystem partitionFileSystem = new PartitionFileSystem(storage2);
			using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
			using LibHac.Fs.Path path = new LibHac.Fs.Path();
			path.Initialize(new U8Span(Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
			partitionFileSystem.OpenFile(ref uniqueRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
			PartitionFileSystem partitionFileSystem2 = new PartitionFileSystem(uniqueRef.Release().AsStorage());
			fileSystem = partitionFileSystem2;
		}
		else
		{
			PartitionFileSystem partitionFileSystem3 = new PartitionFileSystem(storage);
			fileSystem = partitionFileSystem3;
		}
		List<DirectoryEntryEx> list = fileSystem.EnumerateEntries().ToList();
		Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
		foreach (DirectoryEntryEx item2 in list)
		{
			if (!item2.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			try
			{
				using UniqueRef<IFile> uniqueRef2 = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path2 = new LibHac.Fs.Path();
				path2.Initialize(new U8Span(Encoding.UTF8.GetBytes(item2.FullPath))).ThrowIfFailure();
				if (!fileSystem.OpenFile(ref uniqueRef2.Ref, in path2, OpenMode.Read).IsSuccess())
				{
					continue;
				}
				using IFile file = uniqueRef2.Release();
				IStorage storage3 = file.AsStorage();
				storage3.GetSize(out var size2).ThrowIfFailure();
				byte[] array = new byte[size2];
				storage3.Read(0L, array).ThrowIfFailure();
				using MemoryStream stream2 = new MemoryStream(array);
				Ticket ticket = new Ticket(stream2);
				byte[] titleKey = ticket.GetTitleKey(_keysService.CurrentKeyset);
				string key = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
				dictionary[key] = titleKey;
			}
			catch (Exception ex)
			{
				App.Logger.Log("[CatalogScanner] [WARNING] Error reading key from " + item2.Name + ": " + ex.Message, LogLevel.Warning);
			}
		}
		IStorage solidStorage = null;
		IFile file2 = null;
		DirectoryEntryEx directoryEntryEx = list.FirstOrDefault((DirectoryEntryEx e) => e.Name.EndsWith(".solid", StringComparison.OrdinalIgnoreCase));
		if (directoryEntryEx != null)
		{
			try
			{
				using UniqueRef<IFile> uniqueRef3 = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path3 = new LibHac.Fs.Path();
				path3.Initialize(new U8Span(Encoding.UTF8.GetBytes(directoryEntryEx.FullPath))).ThrowIfFailure();
				if (fileSystem.OpenFile(ref uniqueRef3.Ref, in path3, OpenMode.Read).IsSuccess())
				{
					file2 = uniqueRef3.Release();
					solidStorage = file2.AsStorage();
				}
			}
			catch (Exception ex2)
			{
				App.Logger.Log("[CatalogScanner] [WARNING] Error opening solid storage: " + ex2.Message, LogLevel.Warning);
			}
		}
		try
		{
			foreach (DirectoryEntryEx item3 in list)
			{
				token.ThrowIfCancellationRequested();
				string name = item3.Name;
				bool flag2 = name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase);
				bool flag3 = name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);
				if (!(flag2 || flag3))
				{
					continue;
				}
				using UniqueRef<IFile> uniqueRef4 = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path4 = new LibHac.Fs.Path();
				path4.Initialize(new U8Span(Encoding.UTF8.GetBytes(item3.FullPath))).ThrowIfFailure();
				fileSystem.OpenFile(ref uniqueRef4.Ref, in path4, OpenMode.Read).ThrowIfFailure();
				using IFile file3 = uniqueRef4.Release();
				IStorage storage4 = file3.AsStorage();
				IDisposable disposable = null;
				if (flag3)
				{
					try
					{
						StormNczStorage stormNczStorage = new StormNczStorage(storage4, dictionary, solidStorage);
						storage4 = stormNczStorage;
						disposable = stormNczStorage;
					}
					catch (Exception ex3)
					{
						App.Logger.Log("[CatalogScanner] Error opening StormNczStorage for " + name + ": " + ex3.Message, LogLevel.Error);
						goto end_IL_04b4;
					}
				}
				try
				{
					Nca nca = new Nca(_keysService.CurrentKeyset, storage4);
					if (nca.Header.ContentType == NcaContentType.Control)
					{
						IFileSystem fileSystem2 = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);
						using UniqueRef<IFile> uniqueRef5 = default(UniqueRef<IFile>);
						using LibHac.Fs.Path path5 = new LibHac.Fs.Path();
						path5.Initialize(new U8Span(Encoding.UTF8.GetBytes("/control.nacp"))).ThrowIfFailure();
						U8Span value;
						if (fileSystem2.OpenFile(ref uniqueRef5.Ref, in path5, OpenMode.Read).IsSuccess())
						{
							Stream stream3 = uniqueRef5.Release().AsStream();
							ApplicationControlProperty nacp = default(ApplicationControlProperty);
							stream3.Read(MemoryMarshal.AsBytes(new Span<ApplicationControlProperty>(ref nacp)));
							value = nacp.Title[0].NameString;
							string title = value.ToString();
							value = nacp.Title[0].PublisherString;
							string pub = value.ToString();
							if (string.IsNullOrWhiteSpace(title))
							{
								for (int num = 0; num < 16; num++)
								{
									value = nacp.Title[num].NameString;
									string text = value.ToString();
									if (!string.IsNullOrWhiteSpace(text))
									{
										title = text;
										break;
									}
								}
							}
							if (string.IsNullOrWhiteSpace(pub))
							{
								for (int num2 = 0; num2 < 16; num2++)
								{
									value = nacp.Title[num2].PublisherString;
									string text2 = value.ToString();
									if (!string.IsNullOrWhiteSpace(text2))
									{
										pub = text2;
										break;
									}
								}
							}
							string titleIdHex = nca.Header.TitleId.ToString("X16");
							if (titleIdHex.EndsWith("8"))
							{
								titleIdHex = titleIdHex.Substring(0, 15) + "0";
							}
							string supportedLangs = "";
							try
							{
								List<string> list2 = new List<string>();
								uint supportedLanguageFlag = nacp.SupportedLanguageFlag;
								string[] array2 = new string[16]
								{
									"AmericanEnglish", "BritishEnglish", "Japanese", "French", "German", "LatinAmericanSpanish", "Spanish", "Italian", "Dutch", "CanadianFrench",
									"Portuguese", "Russian", "Korean", "TraditionalChinese", "SimplifiedChinese", "BrazilianPortuguese"
								};
								for (int num3 = 0; num3 < array2.Length; num3++)
								{
									if ((supportedLanguageFlag & (uint)(1 << num3)) != 0)
									{
										list2.Add(array2[num3].Replace("American", "").Replace("British", "").Replace("LatinAmerican", ""));
									}
								}
								supportedLangs = string.Join(", ", list2.Distinct());
							}
							catch
							{
							}
							string rating = "Нет данных";
							try
							{
								if (nacp.RatingAge[2] >= 0 && nacp.RatingAge[2] < 31)
								{
									rating = $"PEGI {nacp.RatingAge[2]}+";
								}
								else if (nacp.RatingAge[1] >= 0 && nacp.RatingAge[1] < 31)
								{
									rating = $"ESRB {nacp.RatingAge[1]}+";
								}
							}
							catch
							{
							}
							string videoCap = "Неизвестно";
							try
							{
								videoCap = ((nacp.VideoCapture == ApplicationControlProperty.VideoCaptureValue.Enable) ? "Да" : "Нет");
							}
							catch
							{
							}
							string saveSize = "Неизвестно";
							try
							{
								saveSize = ProcessingTask.FormatSize(nacp.UserAccountSaveDataSize);
							}
							catch
							{
							}
							App.MainDispatcher?.TryEnqueue(delegate
							{
								bool flag4 = titleIdHex.EndsWith("800");
								string titleId = titleIdHex;
								if (flag4)
								{
									titleId = titleIdHex.Substring(0, 13) + "000";
								}
								if (!flag4 && item.Version != "v0" && item.Version != "0")
								{
									if (item.TitleId == "0000000000000000")
									{
										item.TitleId = titleId;
									}
									if (item.TitleName == "Unknown Game" && !string.IsNullOrWhiteSpace(title))
									{
										item.TitleName = title;
									}
								}
								else
								{
									item.TitleName = (string.IsNullOrWhiteSpace(title) ? "Unknown" : title);
									item.Publisher = (string.IsNullOrWhiteSpace(pub) ? "Unknown" : pub);
									item.Version = nacp.DisplayVersionString.ToString();
									item.TitleId = titleId;
									item.SupportedLanguages = (string.IsNullOrEmpty(supportedLangs) ? "Неизвестно" : supportedLangs);
									item.RatingAge = rating;
									item.VideoCapture = videoCap;
									item.SaveDataSize = saveSize;
									string text4 = "";
									if (!string.IsNullOrEmpty(supportedLangs))
									{
										bool flag5 = supportedLangs.Contains("English");
										bool flag6 = supportedLangs.Contains("French") || supportedLangs.Contains("German") || supportedLangs.Contains("Italian") || supportedLangs.Contains("Spanish") || supportedLangs.Contains("Dutch");
										bool flag7 = supportedLangs.Contains("Japanese");
										bool flag8 = supportedLangs.Contains("Korean");
										bool flag9 = supportedLangs.Contains("Chinese");
										if (flag5 && flag6 && flag7)
										{
											text4 = "WW";
										}
										else if (flag5 && flag6)
										{
											text4 = "US/EU";
										}
										else if (flag5 && !flag6 && !flag7)
										{
											text4 = "US";
										}
										else if (!flag5 && flag6)
										{
											text4 = "EU";
										}
										else if (flag7 && !flag5 && !flag6)
										{
											text4 = "JP";
										}
										else if (flag8 && !flag5 && !flag6 && !flag7)
										{
											text4 = "KR";
										}
										else if (flag9 && !flag5 && !flag6 && !flag7)
										{
											text4 = "AS";
										}
									}
									if (string.IsNullOrEmpty(text4))
									{
										text4 = "UNKNOWN";
									}
									item.Regions = text4;
								}
							});
						}
						string[] array3 = new string[15]
						{
							"AmericanEnglish", "BritishEnglish", "Japanese", "French", "German", "LatinAmericanSpanish", "Spanish", "Italian", "Dutch", "CanadianFrench",
							"Portuguese", "Russian", "Korean", "TraditionalChinese", "SimplifiedChinese"
						};
						string[] array4 = array3;
						foreach (string text3 in array4)
						{
							using UniqueRef<IFile> uniqueRef6 = default(UniqueRef<IFile>);
							LibHac.Fs.Path path6 = new LibHac.Fs.Path();
							try
							{
								value = new U8Span(Encoding.UTF8.GetBytes("/icon_" + text3 + ".dat"));
								path6.Initialize(value).ThrowIfFailure();
								if (!fileSystem2.OpenFile(ref uniqueRef6.Ref, in path6, OpenMode.Read).IsSuccess())
								{
									continue;
								}
								using (MemoryStream memoryStream = new MemoryStream())
								{
									uniqueRef6.Release().AsStream().CopyTo(memoryStream);
									memoryStream.Position = 0L;
									byte[] buffer = memoryStream.ToArray();
									App.MainDispatcher?.TryEnqueue(async delegate
									{
										try
										{
											BitmapImage bmp = new BitmapImage();
											using MemoryStream ms = new MemoryStream(buffer);
											await bmp.SetSourceAsync(ms.AsRandomAccessStream());
											item.CoverImage = bmp;
											item.CoverBytes = buffer;
										}
										catch
										{
										}
									});
								}
								break;
							}
							finally
							{
								path6.Dispose();
							}
						}
						App.MainDispatcher?.TryEnqueue(delegate
						{
							item.IsLoading = false;
						});
					}
					else if (nca.Header.ContentType == NcaContentType.Manual)
					{
						try
						{
							IFileSystem fileSystem3 = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);
							int num5 = 0;
							foreach (DirectoryEntryEx item4 in fileSystem3.EnumerateEntries("/", "*.jpg").Concat(fileSystem3.EnumerateEntries("/", "*.png")))
							{
								if (num5 >= 5)
								{
									break;
								}
								using UniqueRef<IFile> uniqueRef7 = default(UniqueRef<IFile>);
								using LibHac.Fs.Path path7 = new LibHac.Fs.Path();
								path7.Initialize(new U8Span(Encoding.UTF8.GetBytes(item4.FullPath))).ThrowIfFailure();
								if (!fileSystem3.OpenFile(ref uniqueRef7.Ref, in path7, OpenMode.Read).IsSuccess())
								{
									continue;
								}
								using MemoryStream memoryStream2 = new MemoryStream();
								uniqueRef7.Release().AsStream().CopyTo(memoryStream2);
								byte[] buffer2 = memoryStream2.ToArray();
								App.MainDispatcher?.TryEnqueue(async delegate
								{
									try
									{
										BitmapImage bmp = new BitmapImage();
										using MemoryStream ms = new MemoryStream(buffer2);
										await bmp.SetSourceAsync(ms.AsRandomAccessStream());
										item.Screenshots.Add(bmp);
										item.HasScreenshots = true;
									}
									catch
									{
									}
								});
								num5++;
							}
						}
						catch
						{
						}
					}
					else
					{
						if (nca.Header.ContentType != NcaContentType.Meta)
						{
							continue;
						}
						try
						{
							IFileSystem fileSystem4 = nca.OpenFileSystem(0, IntegrityCheckLevel.ErrorOnInvalid);
							foreach (DirectoryEntryEx item5 in fileSystem4.EnumerateEntries())
							{
								if (!item5.Name.EndsWith(".cnmt", StringComparison.OrdinalIgnoreCase))
								{
									continue;
								}
								using (UniqueRef<IFile> uniqueRef8 = default(UniqueRef<IFile>))
								{
									using LibHac.Fs.Path path8 = new LibHac.Fs.Path();
									path8.Initialize(new U8Span(Encoding.UTF8.GetBytes(item5.FullPath))).ThrowIfFailure();
									fileSystem4.OpenFile(ref uniqueRef8.Ref, in path8, OpenMode.Read).ThrowIfFailure();
									using MemoryStream memoryStream3 = new MemoryStream();
									uniqueRef8.Release().AsStream().CopyTo(memoryStream3);
									byte[] array5 = memoryStream3.ToArray();
									uint version = 0u;
									try
									{
										if (array5.Length >= 12)
										{
											version = BitConverter.ToUInt32(array5, 8);
										}
									}
									catch
									{
									}
									App.MainDispatcher?.TryEnqueue(delegate
									{
										uint result = 0u;
										uint.TryParse(item.VersionCode, out result);
										Match match = Regex.Match(item.FileName ?? "", "\\[v(\\d+)\\]");
										if (match.Success && uint.TryParse(match.Groups[1].Value, out var result2) && result2 > result)
										{
											result = result2;
										}
										if (version > result)
										{
											item.VersionCode = version.ToString();
										}
										else if (result != 0)
										{
											item.VersionCode = result.ToString();
										}
									});
								}
								break;
							}
						}
						catch
						{
							App.MainDispatcher?.TryEnqueue(delegate
							{
								if (item.VersionCode == "0")
								{
									item.VersionCode = "ERR_META";
								}
							});
						}
						continue;
					}
				}
				catch (Exception ex4)
				{
					App.Logger.Log($"[CatalogScanner] Ошибка парсинга {name}: {ex4.Message}\n{ex4.StackTrace}", LogLevel.Error);
				}
				finally
				{
					if (disposable != null)
					{
						try
						{
							disposable.Dispose();
						}
						catch
						{
						}
					}
				}
				end_IL_04b4:;
			}
		}
		finally
		{
			if (file2 != null)
			{
				try
				{
					file2.Dispose();
				}
				catch
				{
				}
			}
		}
		App.MainDispatcher?.TryEnqueue(delegate
		{
			App.TitleDb.EnrichCatalogItem(item);
			item.IsLoading = false;
		});
	}
}
