#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Common;

namespace steamedit_Enhance.Data;

/// <summary>
/// 在本地 Steam 缓存中查找某个 App 的封面图（只做路径解析，不负责解码/下载）。
/// 解析结果按 AppId + 类别缓存（含“未找到”的负缓存），同一 App 同一会话内不重复扫盘。
/// 优先级约定：竖版封面（2:3，官方多为 300x450 / 600x900 / 自定义 xxxp）优先，横版/横幅仅作为没有竖版时的后备。
/// </summary>
internal sealed class SteamCoverResolver
{
	private static readonly string[] s_extensions = { ".jpg", ".png" };
	private static readonly string[] s_fuzzyExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif" };

	// 目标宽高比：官方竖版为 2:3（常见 300x450 / 600x900 / 1200x1800 同比例多档），横版取 920x430 胶囊图（库里只有 hero ~3.1 时也能接受）
	private const double PortraitTargetAspect = 2.0 / 3.0;
	private const double WideTargetAspect = 920.0 / 430.0;

	private readonly string _steamDir;
	private readonly Dictionary<int, string?> _portraitCache = new();
	private readonly Dictionary<int, string?> _wideCache = new();
	private string[] _userGridDirs = Array.Empty<string>();

	public SteamCoverResolver(string steamDir)
	{
		_steamDir = steamDir ?? "";
	}

	/// <summary>
	/// 竖版封面路径：自定义 xxxp → 官方 library_600x900 → 模糊扫描 appid 目录按宽高比识别竖图。找不到返回 null。
	/// </summary>
	public string? GetPortraitPath(int appId)
	{
		if (appId <= 0 || string.IsNullOrWhiteSpace(_steamDir))
			return null;
		if (_portraitCache.TryGetValue(appId, out string? cached))
			return cached;
		string? path = ResolvePortrait(appId);
		_portraitCache[appId] = path;
		return path;
	}

	/// <summary>
	/// 横版/横幅后备路径：自定义宽胶囊 → library_capsule → library_hero → 模糊扫描 appid 目录按宽高比识别横图。找不到返回 null。
	/// </summary>
	public string? GetWidePath(int appId)
	{
		if (appId <= 0 || string.IsNullOrWhiteSpace(_steamDir))
			return null;
		if (_wideCache.TryGetValue(appId, out string? cached))
			return cached;
		string? path = ResolveWide(appId);
		_wideCache[appId] = path;
		return path;
	}

	private string? ResolvePortrait(int appId)
	{
		string id = appId.ToString();
		foreach (string gridDir in GetUserGridDirs())
		{
			string? custom = FirstExisting(Path.Combine(gridDir, id + "p"));
			if (custom != null) return custom;
		}
		string? official = FirstExistingFromLibraryCache(id, "library_600x900");
		if (official != null) return official;
		// 官方命名找不到时，模糊扫描 appid 目录，按宽高比识别竖封面
		return PickBestFromLibraryCache(id, wantPortrait: true);
	}

	private string? ResolveWide(int appId)
	{
		string id = appId.ToString();
		foreach (string gridDir in GetUserGridDirs())
		{
			string? custom = FirstExisting(Path.Combine(gridDir, id));
			if (custom != null) return custom;
		}
		string? capsule = FirstExistingFromLibraryCache(id, "library_capsule");
		if (capsule != null) return capsule;
		string? hero = FirstExistingFromLibraryCache(id, "library_hero");
		if (hero != null) return hero;
		// 官方命名找不到时，模糊扫描 appid 目录，按宽高比识别横封面
		return PickBestFromLibraryCache(id, wantPortrait: false);
	}

	/// <summary>先试新版 {appId}\asset 布局，再试旧版 {appId}_asset 平铺布局。</summary>
	private string? FirstExistingFromLibraryCache(string id, string asset)
	{
		string libraryCache = Path.Combine(_steamDir, "appcache", "librarycache");
		if (!Directory.Exists(libraryCache))
			return null;
		string? folder = FirstExisting(Path.Combine(libraryCache, id, asset));
		if (folder != null) return folder;
		return FirstExisting(Path.Combine(libraryCache, id + "_" + asset));
	}

	private string? FirstExisting(string pathWithoutExtension)
	{
		foreach (string ext in s_extensions)
		{
			string candidate = pathWithoutExtension + ext;
			try
			{
				if (File.Exists(candidate))
					return candidate;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
			{
				// 忽略无法访问的目录，继续尝试其它候选
			}
		}
		return null;
	}

	/// <summary>
	/// 官方固定文件名没命中时，扫描 {Steam}\appcache\librarycache\{appid}\ 下的所有图片文件，
	/// 读取图片头解析尺寸，选出最接近目标宽高比的方向（竖/横）。无匹配返回 null。
	/// </summary>
	private string? PickBestFromLibraryCache(string id, bool wantPortrait)
	{
		string appDir = Path.Combine(_steamDir, "appcache", "librarycache", id);
		string[] files;
		try
		{
			if (!Directory.Exists(appDir))
				return null;
			files = Directory.GetFiles(appDir);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			return null;
		}
		if (files.Length == 0)
			return null;

		string? best = null;
		double bestScore = double.MaxValue;
		long bestArea = 0;
		double target = wantPortrait ? PortraitTargetAspect : WideTargetAspect;

		foreach (string file in files)
		{
			string name = Path.GetFileName(file);
			// 只接受已知图片扩展名或无扩展名的文件（Steam 部分封面是 hash 命名、无扩展名）
			string ext = Path.GetExtension(name);
			bool isImageName = ext.Length == 0;
			if (!isImageName)
			{
				foreach (string known in s_fuzzyExtensions)
				{
					if (string.Equals(ext, known, StringComparison.OrdinalIgnoreCase))
					{
						isImageName = true;
						break;
					}
				}
			}
			if (!isImageName)
				continue;

			if (!TryReadImageSize(file, out int w, out int h) || w <= 0 || h <= 0)
				continue;
			if (w == h)
				continue;
			bool isPortrait = h > w;
			if (isPortrait != wantPortrait)
				continue;

			double score = Math.Abs((double)w / h - target);
			long area = (long)w * h;
			// 宽高比更接近者优先；宽高比相同（如同比例多档 300x450/600x900）时取更高分辨率
			bool betterByAspect = score < bestScore - 1e-9;
			bool equalAspectBigger = Math.Abs(score - bestScore) <= 1e-9 && area > bestArea;
			if (betterByAspect || equalAspectBigger)
			{
				bestScore = score;
				bestArea = area;
				best = file;
			}
		}
		return best;
	}

	/// <summary>仅读取图片文件头解析宽高，不解码整图。支持 PNG/JPEG/BMP/GIF/WebP。解析失败返回 false。</summary>
	private static bool TryReadImageSize(string path, out int width, out int height)
	{
		width = 0;
		height = 0;
		try
		{
			using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			if (fs.Length < 12)
				return false;
			return TryReadPngSize(fs, ref width, ref height)
				|| TryReadJpegSize(fs, ref width, ref height)
				|| TryReadGifSize(fs, ref width, ref height)
				|| TryReadBmpSize(fs, ref width, ref height)
				|| TryReadWebpSize(fs, ref width, ref height);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			return false;
		}
	}

	private static bool TryReadPngSize(FileStream fs, ref int width, ref int height)
	{
		byte[] sig = new byte[8];
		if (fs.Read(sig, 0, sig.Length) < sig.Length)
			return false;
		if (sig[0] != 0x89 || sig[1] != 0x50 || sig[2] != 0x4E || sig[3] != 0x47 ||
			sig[4] != 0x0D || sig[5] != 0x0A || sig[6] != 0x1A || sig[7] != 0x0A)
			return false;
		// 签名后：长度(4) + "IHDR"(4) + 宽(4) + 高(4)
		if (fs.Length < 24)
			return false;
		byte[] buf = new byte[16];
		if (fs.Read(buf, 0, buf.Length) < buf.Length)
			return false;
		if (buf[4] != (byte)'I' || buf[5] != (byte)'H' || buf[6] != (byte)'D' || buf[7] != (byte)'R')
			return false;
		width = (buf[8] << 24) | (buf[9] << 16) | (buf[10] << 8) | buf[11];
		height = (buf[12] << 24) | (buf[13] << 16) | (buf[14] << 8) | buf[15];
		return true;
	}

	private static bool TryReadJpegSize(FileStream fs, ref int width, ref int height)
	{
		if (fs.Length < 4)
			return false;
		fs.Position = 0;
		if (fs.ReadByte() != 0xFF || fs.ReadByte() != 0xD8)
			return false;
		while (true)
		{
			int b;
			// 寻找段起始 0xFF
			do
			{
				b = fs.ReadByte();
				if (b < 0)
					return false;
			} while (b != 0xFF);
			// 跳过填充 0xFF，得到标记码
			int marker;
			do
			{
				marker = fs.ReadByte();
				if (marker < 0)
					return false;
			} while (marker == 0xFF);
			if (marker == 0xD9 || marker == 0xDA) // EOI / SOS：之前没遇到 SOF
				return false;
			if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
				continue;
			int hi = fs.ReadByte();
			int lo = fs.ReadByte();
			if (hi < 0 || lo < 0)
				return false;
			int segLen = (hi << 8) | lo;
			if (segLen < 2)
				return false;
			bool isSof = (marker >= 0xC0 && marker <= 0xC3) ||
				(marker >= 0xC5 && marker <= 0xC7) ||
				(marker >= 0xC9 && marker <= 0xCB) ||
				(marker >= 0xCD && marker <= 0xCF);
			if (isSof)
			{
				int ph = fs.ReadByte(); // precision
				int hh = fs.ReadByte();
				int hl = fs.ReadByte();
				int wh = fs.ReadByte();
				int wl = fs.ReadByte();
				if (ph < 0 || hh < 0 || hl < 0 || wh < 0 || wl < 0)
					return false;
				height = (hh << 8) | hl;
				width = (wh << 8) | wl;
				return height > 0 && width > 0;
			}
			long remaining = segLen - 2L;
			if (remaining > fs.Length - fs.Position)
				return false;
			if (remaining > 0)
				fs.Position += remaining;
		}
	}

	private static bool TryReadGifSize(FileStream fs, ref int width, ref int height)
	{
		if (fs.Length < 10)
			return false;
		fs.Position = 0;
		byte[] buf = new byte[6];
		if (fs.Read(buf, 0, buf.Length) < buf.Length)
			return false;
		bool ok = (buf[0] == 'G' && buf[1] == 'I' && buf[2] == 'F' && buf[3] == '8' &&
			(buf[4] == '7' || buf[4] == '9') && buf[5] == 'a');
		if (!ok)
			return false;
		fs.Position = 6;
		width = ReadLe16(fs);
		height = ReadLe16(fs);
		return width > 0 && height > 0;
	}

	private static bool TryReadBmpSize(FileStream fs, ref int width, ref int height)
	{
		if (fs.Length < 26)
			return false;
		fs.Position = 0;
		if (fs.ReadByte() != 'B' || fs.ReadByte() != 'M')
			return false;
		fs.Position = 18;
		width = ReadLe32(fs);
		height = ReadLe32(fs);
		if (height < 0)
			height = -height; // 顶部朝下的 BMP 高为负值
		return width > 0 && height > 0;
	}

	private static bool TryReadWebpSize(FileStream fs, ref int width, ref int height)
	{
		if (fs.Length < 30)
			return false;
		fs.Position = 0;
		byte[] head = new byte[12];
		if (fs.Read(head, 0, head.Length) < head.Length)
			return false;
		if (head[0] != 'R' || head[1] != 'I' || head[2] != 'F' || head[3] != 'F' ||
			head[8] != 'W' || head[9] != 'E' || head[10] != 'B' || head[11] != 'P')
			return false;
		byte[] fourcc = new byte[4];
		if (fs.Read(fourcc, 0, 4) < 4)
			return false;
		if (fourcc[0] == 'V' && fourcc[1] == 'P' && fourcc[2] == '8' && fourcc[3] == 'L')
		{
			byte[] sig = new byte[5];
			if (fs.Read(sig, 0, sig.Length) < sig.Length)
				return false;
			if (sig[0] != 0x2F)
				return false;
			uint bits = (uint)(sig[1] | (sig[2] << 8) | (sig[3] << 16) | (sig[4] << 24));
			width = (int)((bits & 0x3FFF) + 1);
			height = (int)(((bits >> 14) & 0x3FFF) + 1);
			return width > 0 && height > 0;
		}
		if (fourcc[0] == 'V' && fourcc[1] == 'P' && fourcc[2] == '8' && fourcc[3] == 'X')
		{
			fs.Position = 20; // 12(Riff+WebP) + 8(chunk头) + 1(flags)+3(reserved)
			width = ReadLe24(fs) + 1;
			height = ReadLe24(fs) + 1;
			return width > 0 && height > 0;
		}
		if (fourcc[0] == 'V' && fourcc[1] == 'P' && fourcc[2] == '8' && fourcc[3] == ' ')
		{
			// VP8 有损：帧头3字节 + 同步码 9D 01 2A，其后各 2 字节低 14 位为 宽-1 / 高-1
			fs.Position = 16;
			byte[] sync = new byte[6];
			if (fs.Read(sync, 0, sync.Length) < sync.Length)
				return false;
			if (sync[3] != 0x9D || sync[4] != 0x01 || sync[5] != 0x2A)
				return false;
			byte[] dims = new byte[4];
			if (fs.Read(dims, 0, dims.Length) < dims.Length)
				return false;
			width = (dims[0] | ((dims[1] & 0x3F) << 8)) + 1;
			height = (dims[2] | ((dims[3] & 0x3F) << 8)) + 1;
			return width > 0 && height > 0;
		}
		return false;
	}

	private static int ReadLe16(FileStream fs)
	{
		int lo = fs.ReadByte();
		int hi = fs.ReadByte();
		if (lo < 0 || hi < 0)
			return 0;
		return lo | (hi << 8);
	}

	private static int ReadLe32(FileStream fs)
	{
		int b0 = fs.ReadByte();
		int b1 = fs.ReadByte();
		int b2 = fs.ReadByte();
		int b3 = fs.ReadByte();
		if (b0 < 0 || b1 < 0 || b2 < 0 || b3 < 0)
			return 0;
		return b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
	}

	private static int ReadLe24(FileStream fs)
	{
		int b0 = fs.ReadByte();
		int b1 = fs.ReadByte();
		int b2 = fs.ReadByte();
		if (b0 < 0 || b1 < 0 || b2 < 0)
			return 0;
		return b0 | (b1 << 8) | (b2 << 16);
	}

	private string[] GetUserGridDirs()
	{
		if (_userGridDirs.Length > 0)
			return _userGridDirs;

		var dirs = new List<string>();
		try
		{
			string userDataRoot = Path.Combine(_steamDir, "userdata");
			if (Directory.Exists(userDataRoot))
			{
				foreach (string userDir in Directory.GetDirectories(userDataRoot))
				{
					if (!long.TryParse(Path.GetFileName(userDir), out _))
						continue;
					string grid = Path.Combine(userDir, "config", "grid");
					if (Directory.Exists(grid))
						dirs.Add(grid);
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			Log.WriteLine("SteamCoverResolver: failed to enumerate userdata: {0}", ex.Message);
		}

		dirs.Sort(StringComparer.Ordinal);
		_userGridDirs = dirs.ToArray();
		return _userGridDirs;
	}
}
