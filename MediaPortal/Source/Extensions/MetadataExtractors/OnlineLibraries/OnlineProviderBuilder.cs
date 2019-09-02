#region Copyright (C) 2007-2018 Team MediaPortal

/*
    Copyright (C) 2007-2018 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using MediaPortal.Common.PluginManager;
using MediaPortal.Common.Services.PluginManager.Builders;

namespace MediaPortal.Extensions.OnlineLibraries
{
  /// <summary>
  /// Plugin item builder for <c>OnlineProviderBuilder</c> plugin items.
  /// </summary>
  public class OnlineProviderBuilder : IPluginItemBuilder
  {
    public const string AUDIO_PROVIDER_PATH = "/Online/AudioProviders";
    public const string MOVIE_PROVIDER_PATH = "/Online/MovieProviders";
    public const string SERIES_PROVIDER_PATH = "/Online/SeriesProviders";
    public const string SUBTITLE_PROVIDER_PATH = "/Online/SubtitleProviders";

    #region IPluginItemBuilder Member

    public object BuildItem(PluginItemMetadata itemData, PluginRuntime plugin)
    {
      BuilderHelper.CheckParameter("ClassName", itemData);
      BuilderHelper.CheckParameter("ProviderName", itemData);
      return new OnlineProviderRegistration(plugin.GetPluginType(itemData.Attributes["ClassName"]), itemData.Id, itemData.Attributes["ProviderName"]);
    }

    public void RevokeItem(object item, PluginItemMetadata itemData, PluginRuntime plugin)
    {
      // Noting to do
    }

    public bool NeedsPluginActive(PluginItemMetadata itemData, PluginRuntime plugin)
    {
      return true;
    }

    #endregion
  }

  /// <summary>
  /// <see cref="OnlineProviderRegistration"/> holds extension metadata.
  /// </summary>
  public class OnlineProviderRegistration
  {
    /// <summary>
    /// Gets the registered type.
    /// </summary>
    public Type ProviderClass { get; private set; }

    /// <summary>
    /// Unique ID of extension.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Name of the thumbnail provider.
    /// </summary>
    public string ProviderName { get; private set; }

    public OnlineProviderRegistration(Type type, string providerId, string providerName)
    {
      ProviderClass = type;
      ProviderName = providerName;
      Id = new Guid(providerId);
    }
  }
}
