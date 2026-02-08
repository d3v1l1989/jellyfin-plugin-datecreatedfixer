using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.DateCreatedFixer;

public class Plugin : BasePlugin<BasePluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "DateCreated Fixer";

    public override Guid Id => Guid.Parse("d8f3a1b2-4c5e-6f78-9a0b-c1d2e3f4a5b6");

    public override string Description =>
        "Fixes invalid DateCreated values (2000-01-01 bug) on movies and episodes by using file LastWriteTimeUtc.";

    public static Plugin? Instance { get; private set; }
}
