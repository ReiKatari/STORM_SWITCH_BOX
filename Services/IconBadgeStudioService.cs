using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Text;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// ?????? ???????????? 3DS ???????? ? Switch ?????? (3D Banner & Animated Audio Studio):
    /// ? ????????? ?????????? ??????? ([RUS], [60 FPS], [MOD], [ALL DLC], ???? ?????) ?? ?????? Switch (256x256) ? 3DS (48x48).
    /// ? ?????? ?????????? Nintendo 3DS SMDH (48x48 + 24x24 Morton Z-order RGB565 ????? + 16 ???????? ?????? UTF-16LE).
    /// ? ????????? 3DS BCWAV (CWAV PCM16) ?? ???????????? .WAV ? ???????? ?????-??????? ? 3DS banner.bin (CBMD).
    /// </summary>
    public static class IconBadgeStudioService
    {
        public static byte[] RenderIconWithBadges(
            byte[]? baseIconBytes,
            int targetSize,
            bool badgeRus,
            bool badge60Fps,
            bool badgeMod,
            bool badgeAllDlc,
            string? customBadge,
            string position = "BottomRight",
            bool outputPng = true)
        {
            using var canvas = new Bitmap(targetSize, targetSize, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(canvas);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (baseIconBytes != null && baseIconBytes.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(baseIconBytes);
                    using var srcImg = Image.FromStream(ms);
                    g.DrawImage(srcImg, new Rectangle(0, 0, targetSize, targetSize));
                }
                catch
                {
                    DrawFallbackIconBackground(g, targetSize);
                }
            }
            else
            {
                DrawFallbackIconBackground(g, targetSize);
            }

            var badges = new List<(string Text, Color BgStart, Color BgEnd)>();
            if (badgeRus) badges.Add(("RUS", Color.FromArgb(235, 225, 29, 72), Color.FromArgb(235, 159, 18, 57)));
            if (badge60Fps) badges.Add(("60FPS", Color.FromArgb(235, 16, 185, 129), Color.FromArgb(235, 4, 120, 87)));
            if (badgeMod) badges.Add(("MOD", Color.FromArgb(235, 124, 58, 237), Color.FromArgb(235, 91, 33, 182)));
            if (badgeAllDlc) badges.Add(("DLC", Color.FromArgb(235, 217, 119, 6), Color.FromArgb(235, 180, 83, 9)));
            if (!string.IsNullOrWhiteSpace(customBadge))
            {
                string clean = customBadge.Trim();
                if (clean.Length > 10) clean = clean.Substring(0, 10);
                badges.Add((clean.ToUpperInvariant(), Color.FromArgb(235, 2, 132, 199), Color.FromArgb(235, 3, 105, 161)));
            }

            if (badges.Count > 0)
            {
                if (string.Equals(position, "BottomBar", StringComparison.OrdinalIgnoreCase))
                {
                    DrawBottomRibbon(g, targetSize, badges);
                }
                else
                {
                    DrawCornerPills(g, targetSize, badges, position);
                }
            }

            using var outMs = new MemoryStream();
            if (outputPng)
            {
                canvas.Save(outMs, ImageFormat.Png);
                return outMs.ToArray();
            }
            else
            {
                canvas.Save(outMs, ImageFormat.Png);
                return SmartShrinkService.OptimizeSwitchIconJpeg(outMs.ToArray());
            }
        }

        private static void DrawFallbackIconBackground(Graphics g, int size)
        {
            using var brush = new LinearGradientBrush(
                new Rectangle(0, 0, size, size),
                Color.FromArgb(255, 30, 41, 59),
                Color.FromArgb(255, 15, 23, 42),
                45f);
            g.FillRectangle(brush, 0, 0, size, size);
        }

        private static void DrawBottomRibbon(Graphics g, int size, List<(string Text, Color BgStart, Color BgEnd)> badges)
        {
            string combined = string.Join(" ? ", badges.ConvertAll(b => b.Text));
            int barHeight = Math.Max(12, (int)(size * 0.19f));
            int y = size - barHeight;

            using var barBrush = new LinearGradientBrush(
                new Rectangle(0, y, size, barHeight),
                badges[0].BgStart,
                badges[badges.Count - 1].BgEnd,
                0f);
            g.FillRectangle(barBrush, 0, y, size, barHeight);

            using var topPen = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(1f, size / 128f));
            g.DrawLine(topPen, 0, y, size, y);

            float fontSize = Math.Max(6.5f, barHeight * 0.54f);
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var shadowBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            using var textBrush = new SolidBrush(Color.White);

            g.DrawString(combined, font, shadowBrush, new RectangleF(1, y + 1, size, barHeight), sf);
            g.DrawString(combined, font, textBrush, new RectangleF(0, y, size, barHeight), sf);
        }

        private static void DrawCornerPills(Graphics g, int size, List<(string Text, Color BgStart, Color BgEnd)> badges, string position)
        {
            bool isRight = position.Contains("Right", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(position);
            bool isBottom = position.Contains("Bottom", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(position);

            float scale = size / 256f;
            float margin = Math.Max(2f, 8f * scale);
            float spacing = Math.Max(2f, 5f * scale);
            float pillHeight = Math.Max(11f, 34f * scale);
            float fontSize = Math.Max(7f, 19f * scale);
            float radius = Math.Max(3f, 8f * scale);

            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            float currentY = isBottom ? (size - margin - pillHeight) : margin;

            foreach (var badge in badges)
            {
                SizeF textSize = g.MeasureString(badge.Text, font);
                float padX = Math.Max(4f, 12f * scale);
                float pillWidth = Math.Min(size - margin * 2, Math.Max(pillHeight * 1.3f, textSize.Width + padX));
                float x = isRight ? (size - margin - pillWidth) : margin;

                var rect = new RectangleF(x, currentY, pillWidth, pillHeight);
                var shadowRect = new RectangleF(x + Math.Max(1f, 2f * scale), currentY + Math.Max(1f, 2f * scale), pillWidth, pillHeight);

                using (var shadowPath = CreateRoundedRectangle(shadowRect, radius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }

                using (var path = CreateRoundedRectangle(rect, radius))
                using (var bgBrush = new LinearGradientBrush(rect, badge.BgStart, badge.BgEnd, 90f))
                using (var borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), Math.Max(1f, 1.8f * scale)))
                {
                    g.FillPath(bgBrush, path);
                    g.DrawPath(borderPen, path);
                }

                using (var textShadow = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    g.DrawString(badge.Text, font, textShadow, new RectangleF(rect.X + 1f, rect.Y + 1f, rect.Width, rect.Height), sf);
                    g.DrawString(badge.Text, font, textBrush, rect, sf);
                }

                if (isBottom)
                {
                    currentY -= (pillHeight + spacing);
                    if (currentY < 0) break;
                }
                else
                {
                    currentY += (pillHeight + spacing);
                    if (currentY + pillHeight > size) break;
                }
            }
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 1f)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// ????????? ??????????? 3DS SMDH ???? (0x36C0 ????) ? 16 ??????? UTF-16LE ????????
        /// ? ????? ???????? (24x24 ? 48x48) ? ??????? Morton Z-order RGB565.
        /// </summary>
        public static byte[] BuildSmdhBytes(
            byte[]? existingSmdh,
            string shortTitle,
            string longTitle,
            string publisher,
            byte[]? iconImageBytes)
        {
            byte[] smdh = (existingSmdh != null && existingSmdh.Length >= 0x36C0)
                ? (byte[])existingSmdh.Clone()
                : new byte[0x36C0];

            // Magic "SMDH"
            smdh[0] = (byte)'S';
            smdh[1] = (byte)'M';
            smdh[2] = (byte)'D';
            smdh[3] = (byte)'H';

            // If new SMDH, set default Region Lock Free (0x7FFFFFFF at 0x2018) and visible/rating flags
            if (existingSmdh == null || existingSmdh.Length < 0x36C0)
            {
                Array.Copy(BitConverter.GetBytes(0x7FFFFFFFu), 0, smdh, 0x2018, 4); // Region Free
                Array.Copy(BitConverter.GetBytes(0x0000000Fu), 0, smdh, 0x2028, 4); // Visible + Record Usage
            }

            string sTitle = string.IsNullOrWhiteSpace(shortTitle) ? "3DS Game" : shortTitle.Trim();
            string lTitle = string.IsNullOrWhiteSpace(longTitle) ? sTitle : longTitle.Trim();
            string pub = string.IsNullOrWhiteSpace(publisher) ? "Nintendo" : publisher.Trim();

            byte[] shortBytes = Encoding.Unicode.GetBytes(sTitle);
            byte[] longBytes = Encoding.Unicode.GetBytes(lTitle);
            byte[] pubBytes = Encoding.Unicode.GetBytes(pub);

            for (int slot = 0; slot < 16; slot++)
            {
                int slotOffset = 0x08 + slot * 0x200;
                Array.Clear(smdh, slotOffset, 0x200);
                Array.Copy(shortBytes, 0, smdh, slotOffset, Math.Min(shortBytes.Length, 0x7E));
                Array.Copy(longBytes, 0, smdh, slotOffset + 0x80, Math.Min(longBytes.Length, 0xFE));
                Array.Copy(pubBytes, 0, smdh, slotOffset + 0x180, Math.Min(pubBytes.Length, 0x7E));
            }

            if (iconImageBytes != null && iconImageBytes.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(iconImageBytes);
                    using var srcImg = Image.FromStream(ms);

                    byte[] smallRgb565 = EncodeSmdhTiledRgb565(srcImg, 24);
                    Array.Copy(smallRgb565, 0, smdh, 0x2040, 0x480);

                    byte[] largeRgb565 = EncodeSmdhTiledRgb565(srcImg, 48);
                    Array.Copy(largeRgb565, 0, smdh, 0x24C0, 0x1200);
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[IconStudio] ?????? ??????????? SMDH RGB565 ??????: {ex.Message}", LogLevel.Warning);
                }
            }

            return smdh;
        }

        private static byte[] EncodeSmdhTiledRgb565(Image source, int size)
        {
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Black);
                g.DrawImage(source, new Rectangle(0, 0, size, size));
            }

            int tilesPerSide = size / 8;
            byte[] output = new byte[size * size * 2];
            int outOffset = 0;

            for (int ty = 0; ty < tilesPerSide; ty++)
            {
                for (int tx = 0; tx < tilesPerSide; tx++)
                {
                    for (int py = 0; py < 8; py++)
                    {
                        for (int px = 0; px < 8; px++)
                        {
                            int mortonIdx = Morton2D(px, py);
                            int tileBytePos = outOffset + mortonIdx * 2;
                            Color c = bmp.GetPixel(tx * 8 + px, ty * 8 + py);
                            ushort r5 = (ushort)((c.R >> 3) & 0x1F);
                            ushort g6 = (ushort)((c.G >> 2) & 0x3F);
                            ushort b5 = (ushort)((c.B >> 3) & 0x1F);
                            ushort rgb565 = (ushort)((r5 << 11) | (g6 << 5) | b5);
                            output[tileBytePos] = (byte)(rgb565 & 0xFF);
                            output[tileBytePos + 1] = (byte)((rgb565 >> 8) & 0xFF);
                        }
                    }
                    outOffset += 64 * 2;
                }
            }

            return output;
        }

        private static int Morton2D(int x, int y)
        {
            x = (x | (x << 2)) & 0x33;
            x = (x | (x << 1)) & 0x55;
            y = (y | (y << 2)) & 0x33;
            y = (y | (y << 1)) & 0x55;
            return x | (y << 1);
        }

        /// <summary>
        /// ???????????? ??????????? .WAV (PCM) ??? ??????? .BCWAV ? ?????? Nintendo 3DS BCWAV (CWAV PCM16)
        /// ??? ????????? ??????? ??????? (?? 3 ??????).
        /// </summary>
        public static byte[]? BuildBcwavFromAudioFile(string audioPath)
        {
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath)) return null;

            byte[] raw = File.ReadAllBytes(audioPath);
            if (raw.Length < 44) return null;

            // ???? ??? ??? ??????? BCWAV / CWAV
            if (raw[0] == 'C' && raw[1] == 'W' && raw[2] == 'A' && raw[3] == 'V')
            {
                return raw;
            }

            // ??????? RIFF WAVE
            if (Encoding.ASCII.GetString(raw, 0, 4) != "RIFF" || Encoding.ASCII.GetString(raw, 8, 4) != "WAVE")
                return null;

            int channels = 1;
            int sampleRate = 22050;
            int bitsPerSample = 16;
            byte[]? pcmData = null;

            int pos = 12;
            while (pos + 8 <= raw.Length)
            {
                string chunkId = Encoding.ASCII.GetString(raw, pos, 4);
                int chunkSize = BitConverter.ToInt32(raw, pos + 4);
                pos += 8;
                if (chunkSize < 0 || pos + chunkSize > raw.Length) break;

                if (chunkId == "fmt " && chunkSize >= 16)
                {
                    channels = BitConverter.ToUInt16(raw, pos + 2);
                    sampleRate = BitConverter.ToInt32(raw, pos + 4);
                    bitsPerSample = BitConverter.ToUInt16(raw, pos + 14);
                }
                else if (chunkId == "data")
                {
                    pcmData = new byte[chunkSize];
                    Array.Copy(raw, pos, pcmData, 0, chunkSize);
                    break;
                }
                pos += chunkSize + (chunkSize & 1);
            }

            if (pcmData == null || pcmData.Length == 0) return null;

            // ??????????? ? ???? 16-??? PCM (?? 3 ??????, ????? ?????????????? ?????????? ? ????? 3DS ???????)
            var monoSamples = new List<short>();
            int bytesPerSample = Math.Max(1, bitsPerSample / 8);
            int frameSize = bytesPerSample * Math.Max(1, channels);
            int maxSamples = Math.Min(pcmData.Length / frameSize, sampleRate * 3);

            for (int i = 0; i < maxSamples; i++)
            {
                int offset = i * frameSize;
                if (offset + bytesPerSample > pcmData.Length) break;
                short sample = bitsPerSample == 8
                    ? (short)((pcmData[offset] - 128) << 8)
                    : BitConverter.ToInt16(pcmData, offset);
                monoSamples.Add(sample);
            }

            int numSamples = monoSamples.Count;
            int pcmBytesLen = numSamples * 2;
            int alignedPcmLen = (pcmBytesLen + 0x1F) & ~0x1F;

            // ?????? ???????? ????????? CWAV (Header 0x40 + INFO 0x60 + DATA 0x20 + alignedPcmLen)
            int infoOffset = 0x40;
            int infoSize = 0x60;
            int dataOffset = infoOffset + infoSize;
            int dataSize = 0x20 + alignedPcmLen;
            int totalSize = dataOffset + dataSize;

            byte[] cwav = new byte[totalSize];
            using var ms = new MemoryStream(cwav);
            using var bw = new BinaryWriter(ms);

            // CWAV Header (0x40)
            bw.Write(Encoding.ASCII.GetBytes("CWAV"));
            bw.Write((ushort)0xFEFF); // Little Endian BOM
            bw.Write((ushort)0x0040); // Header size
            bw.Write((uint)0x02010000); // Version 2.1.0.0
            bw.Write((uint)totalSize);
            bw.Write((ushort)2); // 2 blocks (INFO, DATA)
            bw.Write((ushort)0);
            // INFO Ref
            bw.Write((ushort)0x7000);
            bw.Write((ushort)0);
            bw.Write((uint)infoOffset);
            bw.Write((uint)infoSize);
            // DATA Ref
            bw.Write((ushort)0x7001);
            bw.Write((ushort)0);
            bw.Write((uint)dataOffset);
            bw.Write((uint)dataSize);

            // INFO Block at 0x40 (0x60 bytes)
            ms.Position = infoOffset;
            bw.Write(Encoding.ASCII.GetBytes("INFO"));
            bw.Write((uint)infoSize);
            bw.Write((byte)1); // Encoding = 1 (PCM16)
            bw.Write((byte)0); // Loop = false
            bw.Write((ushort)0);
            bw.Write((uint)sampleRate);
            bw.Write((uint)0); // LoopStart
            bw.Write((uint)numSamples); // LoopEnd / FrameCount
            bw.Write((uint)0);
            bw.Write((uint)1); // 1 Channel
            bw.Write((ushort)0x7100); // ChannelInfo ref
            bw.Write((ushort)0);
            bw.Write((uint)0x14); // Offset from ChannelCount to ChannelInfo

            // ChannelInfo at infoOffset + 0x2C
            ms.Position = infoOffset + 0x2C;
            bw.Write((ushort)0x1F00); // Sample data ref inside DATA+0x08
            bw.Write((ushort)0);
            bw.Write((uint)0x18); // Offset inside DATA payload
            bw.Write((ushort)0); // No ADPCM info
            bw.Write((ushort)0);
            bw.Write((uint)0xFFFFFFFF);
            bw.Write((uint)0);

            // DATA Block at 0xA0
            ms.Position = dataOffset;
            bw.Write(Encoding.ASCII.GetBytes("DATA"));
            bw.Write((uint)dataSize);
            ms.Position = dataOffset + 0x20;
            foreach (short s in monoSamples)
            {
                bw.Write(s);
            }

            return cwav;
        }

        /// <summary>
        /// ???????? ???????? ?????? BCWAV ?????? 3DS ????? banner.bin (????????? CBMD).
        /// </summary>
        public static bool InjectBcwavIntoBannerBin(string bannerBinPath, byte[] bcwavBytes)
        {
            if (!File.Exists(bannerBinPath) || bcwavBytes == null || bcwavBytes.Length < 0x40)
                return false;

            try
            {
                byte[] banner = File.ReadAllBytes(bannerBinPath);
                if (banner.Length < 0x88 || Encoding.ASCII.GetString(banner, 0, 4) != "CBMD")
                    return false;

                int cwavOffset = BitConverter.ToInt32(banner, 0x84);
                if (cwavOffset <= 0 || cwavOffset > banner.Length)
                {
                    cwavOffset = (banner.Length + 0x1F) & ~0x1F;
                }

                byte[] newBanner = new byte[cwavOffset + bcwavBytes.Length];
                Array.Copy(banner, 0, newBanner, 0, Math.Min(banner.Length, cwavOffset));
                Array.Copy(bcwavBytes, 0, newBanner, cwavOffset, bcwavBytes.Length);
                Array.Copy(BitConverter.GetBytes((uint)cwavOffset), 0, newBanner, 0x84, 4);

                File.WriteAllBytes(bannerBinPath, newBanner);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[IconStudio] ?????? ???????? BCWAV ? banner.bin: {ex.Message}", LogLevel.Warning);
                return false;
            }
        }

        /// <summary>
        /// ????????? ????????? ??????/??????, ?????????? SMDH ? ????????? ???? ??????? ? ????????????? ?????????? ExeFS 3DS.
        /// </summary>
        public static void ApplyCustom3dsMetadataAndBannerToExtractedDir(GameMetadataEditModel model, string exefsDir, ProcessingTask? task = null)
        {
            if (model == null || !Directory.Exists(exefsDir)) return;

            try
            {
                string iconBinPath = Path.Combine(exefsDir, "icon.bin");
                byte[]? existingSmdh = File.Exists(iconBinPath) ? File.ReadAllBytes(iconBinPath) : null;

                byte[]? iconBytes = model.CustomIconBytes ?? model.OriginalIconBytes;
                if (model.HasBadges && model.CustomIconBytes == null && iconBytes != null)
                {
                    iconBytes = RenderIconWithBadges(
                        iconBytes,
                        48,
                        model.BadgeRus,
                        model.Badge60Fps,
                        model.BadgeMod,
                        model.BadgeAllDlc,
                        model.CustomBadgeText,
                        model.BadgePosition,
                        outputPng: true);
                }

                string title = !string.IsNullOrWhiteSpace(model.TitleNameRussian) ? model.TitleNameRussian : model.TitleNameEnglish;
                byte[] newSmdh = BuildSmdhBytes(existingSmdh, title, title, model.Publisher, iconBytes);
                File.WriteAllBytes(iconBinPath, newSmdh);

                if (task != null)
                {
                    App.RunOnUI(() => task.LogDetails += $"\n?? [3DS Banner Studio] ???????? SMDH ??????? ? ?????? 48x48/24x24: ?{title}?");
                }

                if (!string.IsNullOrWhiteSpace(model.CustomAudioWavPath) && File.Exists(model.CustomAudioWavPath))
                {
                    string bannerBinPath = Path.Combine(exefsDir, "banner.bin");
                    byte[]? bcwav = BuildBcwavFromAudioFile(model.CustomAudioWavPath);
                    if (bcwav != null && File.Exists(bannerBinPath))
                    {
                        if (InjectBcwavIntoBannerBin(bannerBinPath, bcwav) && task != null)
                        {
                            App.RunOnUI(() => task.LogDetails += $"\n?? [3DS Banner Studio] ???? ????????? ?????-?????? ??????? ({Path.GetFileName(model.CustomAudioWavPath)}, {bcwav.Length} ????)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[IconStudio] ?????? ?????????? 3DS ExeFS: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// ??????? in-place ???? SMDH ?????? ???????? .3ds/.cci/.cia/.cxi ????? ??? ?????? ??????????.
        /// </summary>
        public static bool Patch3dsSmdhInPlace(string filePath, GameMetadataEditModel model)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) || model == null) return false;
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                byte[] buf = new byte[64 * 1024];
                long pos = 0;
                long smdhOffset = -1;

                while (pos < fs.Length - 0x36C0)
                {
                    fs.Position = pos;
                    int read = fs.Read(buf, 0, buf.Length);
                    if (read < 4) break;
                    for (int i = 0; i <= read - 4; i++)
                    {
                        if (buf[i] == 'S' && buf[i + 1] == 'M' && buf[i + 2] == 'D' && buf[i + 3] == 'H')
                        {
                            smdhOffset = pos + i;
                            break;
                        }
                    }
                    if (smdhOffset >= 0) break;
                    pos += read - 4;
                }

                if (smdhOffset < 0 || smdhOffset + 0x36C0 > fs.Length) return false;

                fs.Position = smdhOffset;
                byte[] existingSmdh = new byte[0x36C0];
                fs.Read(existingSmdh, 0, 0x36C0);

                byte[]? iconBytes = model.CustomIconBytes ?? model.OriginalIconBytes;
                if (model.HasBadges && model.CustomIconBytes == null && iconBytes != null)
                {
                    iconBytes = RenderIconWithBadges(iconBytes, 48, model.BadgeRus, model.Badge60Fps, model.BadgeMod, model.BadgeAllDlc, model.CustomBadgeText, model.BadgePosition, true);
                }

                string title = !string.IsNullOrWhiteSpace(model.TitleNameRussian) ? model.TitleNameRussian : model.TitleNameEnglish;
                byte[] newSmdh = BuildSmdhBytes(existingSmdh, title, title, model.Publisher, iconBytes);

                fs.Position = smdhOffset;
                fs.Write(newSmdh, 0, 0x36C0);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
