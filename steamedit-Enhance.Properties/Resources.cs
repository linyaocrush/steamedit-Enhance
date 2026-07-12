using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace steamedit_Enhance.Properties;

[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
internal class Resources
{
	private static ResourceManager resourceMan;

	private static CultureInfo resourceCulture;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static ResourceManager ResourceManager
	{
		get
		{
			if (resourceMan == null)
			{
				resourceMan = new ResourceManager("steamedit_Enhance.Properties.Resources", typeof(Resources).Assembly);
			}
			return resourceMan;
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static CultureInfo Culture
	{
		get
		{
			return resourceCulture;
		}
		set
		{
			resourceCulture = value;
		}
	}

	internal static Bitmap base_cog_32 => (Bitmap)ResourceManager.GetObject("base_cog_32", resourceCulture);

	internal static Bitmap filter => (Bitmap)ResourceManager.GetObject("filter", resourceCulture);

	internal static Bitmap filter_application => (Bitmap)ResourceManager.GetObject("filter_application", resourceCulture);

	internal static Bitmap filter_beta => (Bitmap)ResourceManager.GetObject("filter_beta", resourceCulture);

	internal static Bitmap filter_config => (Bitmap)ResourceManager.GetObject("filter_config", resourceCulture);

	internal static Bitmap filter_demo => (Bitmap)ResourceManager.GetObject("filter_demo", resourceCulture);

	internal static Bitmap filter_dlc => (Bitmap)ResourceManager.GetObject("filter_dlc", resourceCulture);

	internal static Bitmap filter_game => (Bitmap)ResourceManager.GetObject("filter_game", resourceCulture);

	internal static Bitmap filter_guide => (Bitmap)ResourceManager.GetObject("filter_guide", resourceCulture);

	internal static Bitmap filter_music => (Bitmap)ResourceManager.GetObject("filter_music", resourceCulture);

	internal static Bitmap filter_tool => (Bitmap)ResourceManager.GetObject("filter_tool", resourceCulture);

	internal static Bitmap filter_unknown => (Bitmap)ResourceManager.GetObject("filter_unknown", resourceCulture);

	internal static Bitmap filter_video => (Bitmap)ResourceManager.GetObject("filter_video", resourceCulture);

	internal static Bitmap icon_completed => (Bitmap)ResourceManager.GetObject("icon_completed", resourceCulture);

	internal static Bitmap icon_hidden => (Bitmap)ResourceManager.GetObject("icon_hidden", resourceCulture);

	internal static Bitmap icon_modified => (Bitmap)ResourceManager.GetObject("icon_modified", resourceCulture);

	internal static Icon logo => (Icon)ResourceManager.GetObject("logo", resourceCulture);

	internal static Bitmap logo1 => (Bitmap)ResourceManager.GetObject("logo1", resourceCulture);

	internal static Bitmap OpenFile => (Bitmap)ResourceManager.GetObject("OpenFile", resourceCulture);

	internal static Bitmap openfolderHS => (Bitmap)ResourceManager.GetObject("openfolderHS", resourceCulture);

	internal static string WebsiteUrl => ResourceManager.GetString("WebsiteUrl", resourceCulture);

	internal Resources()
	{
	}
}
