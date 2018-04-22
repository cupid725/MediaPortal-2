#region Copyright (C) 2007-2017 Team MediaPortal

/*
    Copyright (C) 2007-2017 Team MediaPortal
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

using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Common.Messaging;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.Settings;
using MediaPortal.Extensions.MetadataExtractors.MovieMetadataExtractor.Matchers;
using MediaPortal.Extensions.MetadataExtractors.MovieMetadataExtractor.Settings;
using MediaPortal.Extensions.OnlineLibraries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaPortal.Extensions.MetadataExtractors.MovieMetadataExtractor
{
  /// <summary>
  /// MediaPortal 2 metadata extractor implementation for Movies.
  /// </summary>
  public class MovieMetadataExtractor : IMetadataExtractor, IDisposable
  {
    #region Constants

    /// <summary>
    /// GUID string for the video metadata extractor.
    /// </summary>
    public const string METADATAEXTRACTOR_ID_STR = "C2800928-8A57-4979-A95F-3CE6F3EBAB92";

    /// <summary>
    /// Video metadata extractor GUID.
    /// </summary>
    public static Guid METADATAEXTRACTOR_ID = new Guid(METADATAEXTRACTOR_ID_STR);

    protected const string MEDIA_CATEGORY_NAME_MOVIE = "Movie";
    public const double MINIMUM_HOUR_AGE_BEFORE_UPDATE = 0.5;

    #endregion

    #region Protected fields and classes

    protected static ICollection<MediaCategory> MEDIA_CATEGORIES = new List<MediaCategory>();
    private static readonly ICollection<String> IMG_EXTENSIONS = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ".jpg", ".png", ".tbn" };

    protected MetadataExtractorMetadata _metadata;
    protected AsynchronousMessageQueue _messageQueue;
    protected int _importerCount;
    protected SettingsChangeWatcher<MovieMetadataExtractorSettings> _settingWatcher;

    #endregion

    #region Ctor

    static MovieMetadataExtractor()
    {
      MediaCategory movieCategory;
      IMediaAccessor mediaAccessor = ServiceRegistration.Get<IMediaAccessor>();
      if (!mediaAccessor.MediaCategories.TryGetValue(MEDIA_CATEGORY_NAME_MOVIE, out movieCategory))
        movieCategory = mediaAccessor.RegisterMediaCategory(MEDIA_CATEGORY_NAME_MOVIE, new List<MediaCategory> { DefaultMediaCategories.Video });
      MEDIA_CATEGORIES.Add(movieCategory);
    }

    public MovieMetadataExtractor()
    {
      _metadata = new MetadataExtractorMetadata(METADATAEXTRACTOR_ID, "Movies metadata extractor", MetadataExtractorPriority.External, true,
          MEDIA_CATEGORIES, new MediaItemAspectMetadata[]
              {
                MediaAspect.Metadata,
                MovieAspect.Metadata
              });

      _messageQueue = new AsynchronousMessageQueue(this, new string[]
        {
            ImporterWorkerMessaging.CHANNEL,
        });
      _messageQueue.MessageReceived += OnMessageReceived;
      _messageQueue.Start();

      _settingWatcher = new SettingsChangeWatcher<MovieMetadataExtractorSettings>();
      _settingWatcher.SettingsChanged += SettingsChanged;

      LoadSettings();
    }

    public void Dispose()
    {
      _messageQueue.Shutdown();
    }

    private void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      if (message.ChannelName == ImporterWorkerMessaging.CHANNEL)
      {
        ImporterWorkerMessaging.MessageType messageType = (ImporterWorkerMessaging.MessageType)message.MessageType;
        switch (messageType)
        {
          case ImporterWorkerMessaging.MessageType.ImportStarted:
            if (Interlocked.Increment(ref _importerCount) == 1)
            {
              IMediaFanArtHandler fanartHandler;
              if (ServiceRegistration.Get<IMediaAccessor>().LocalFanArtHandlers.TryGetValue(MovieFanArtHandler.FANARTHANDLER_ID, out fanartHandler))
                fanartHandler.ClearCache();
            }
            break;
        }
      }
    }

    #endregion

    #region Settings

    public static bool SkipOnlineSearches { get; private set; }
    public static bool SkipFanArtDownload { get; private set; }
    public static bool CacheOfflineFanArt { get; private set; }
    public static bool CacheLocalFanArt { get; private set; }
    public static bool IncludeActorDetails { get; private set; }
    public static bool IncludeCharacterDetails { get; private set; }
    public static bool IncludeDirectorDetails { get; private set; }
    public static bool IncludeProductionCompanyDetails { get; private set; }
    public static bool IncludeWriterDetails { get; private set; }
    public static bool OnlyLocalMedia { get; private set; }

    private void LoadSettings()
    {
      SkipOnlineSearches = _settingWatcher.Settings.SkipOnlineSearches;
      SkipFanArtDownload = _settingWatcher.Settings.SkipFanArtDownload;
      CacheOfflineFanArt = _settingWatcher.Settings.CacheOfflineFanArt;
      CacheLocalFanArt = _settingWatcher.Settings.CacheLocalFanArt;
      IncludeActorDetails = _settingWatcher.Settings.IncludeActorDetails;
      IncludeCharacterDetails = _settingWatcher.Settings.IncludeCharacterDetails;
      IncludeDirectorDetails = _settingWatcher.Settings.IncludeDirectorDetails;
      IncludeProductionCompanyDetails = _settingWatcher.Settings.IncludeProductionCompanyDetails;
      IncludeWriterDetails = _settingWatcher.Settings.IncludeWriterDetails;
      OnlyLocalMedia = _settingWatcher.Settings.OnlyLocalMedia;
    }

    private void SettingsChanged(object sender, EventArgs e)
    {
      LoadSettings();
    }

    #endregion

    #region Private methods

    private async Task<bool> ExtractMovieData(ILocalFsResourceAccessor lfsra, IDictionary<Guid, IList<MediaItemAspect>> extractedAspectData)
    {
      // VideoAspect must be present to be sure it is actually a video resource.
      if (!extractedAspectData.ContainsKey(VideoAspect.ASPECT_ID) && !extractedAspectData.ContainsKey(SubtitleAspect.ASPECT_ID))
        return false;

      // Calling EnsureLocalFileSystemAccess not necessary; only string operation
      string[] pathsToTest = new[] { lfsra.LocalFileSystemPath, lfsra.CanonicalLocalResourcePath.ToString() };
      string title = null;
      string sortTitle = null;
      bool isReimport = extractedAspectData.ContainsKey(ReimportAspect.ASPECT_ID);

        MovieInfo movieInfo = new MovieInfo();
      if (extractedAspectData.ContainsKey(MovieAspect.ASPECT_ID))
        movieInfo.FromMetadata(extractedAspectData);

      if (movieInfo.MovieName.IsEmpty)
      {
        //Try to get title
        if (MediaItemAspect.TryGetAttribute(extractedAspectData, MediaAspect.ATTR_TITLE, out title) &&
          !string.IsNullOrEmpty(title) && !lfsra.ResourceName.StartsWith(title, StringComparison.InvariantCultureIgnoreCase))
        {
          //The title may still contain tags and other noise, try and parse it for a title and year.
          MovieNameMatcher.MatchTitleYear(title, movieInfo);
        }
      }
      if (movieInfo.MovieNameSort.IsEmpty)
      {
        //Try to get sort title
        if (MediaItemAspect.TryGetAttribute(extractedAspectData, MediaAspect.ATTR_SORT_TITLE, out sortTitle) && !string.IsNullOrEmpty(sortTitle))
        {
          movieInfo.MovieNameSort = sortTitle;
        }
      }

      if (!isReimport) //Ignore tags or file based information for reimport because they might be the cause of the wrong import
      {
        if (movieInfo.MovieDbId == 0)
        {
          try
          {
            // Try to use an existing TMDB id for exact mapping
            string tmdbId = await MatroskaMatcher.TryMatchTmdbIdAsync(lfsra).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(tmdbId))
              movieInfo.MovieDbId = Convert.ToInt32(tmdbId);
          }
          catch (Exception ex)
          {
            ServiceRegistration.Get<ILogger>().Debug("MoviesMetadataExtractor: Exception reading TMDB ID for '{0}'", ex, lfsra.CanonicalLocalResourcePath);
          }
        }

        if (string.IsNullOrEmpty(movieInfo.ImdbId))
        {
          try
          {
            // Try to use an existing IMDB id for exact mapping
            string imdbId = await MatroskaMatcher.TryMatchImdbIdAsync(lfsra).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(imdbId))
              movieInfo.ImdbId = imdbId;
            else if (pathsToTest.Any(path => ImdbIdMatcher.TryMatchImdbId(path, out imdbId)))
              movieInfo.ImdbId = imdbId;
          }
          catch (Exception ex)
          {
            ServiceRegistration.Get<ILogger>().Debug("MoviesMetadataExtractor: Exception reading IMDB ID for '{0}'", ex, lfsra.CanonicalLocalResourcePath);
          }
        }

        if (!movieInfo.IsBaseInfoPresent || !movieInfo.ReleaseDate.HasValue)
        {
          // Also test the full path year. This is useful if the path contains the real name and year.
          foreach (string path in pathsToTest)
          {
            if (MovieNameMatcher.MatchTitleYear(path, movieInfo))
              break;
          }
          //Fall back to MediaAspect.ATTR_TITLE
          if (movieInfo.MovieName.IsEmpty && !string.IsNullOrEmpty(title))
            movieInfo.MovieName = title;

          /* Clear the names from unwanted strings */
          MovieNameMatcher.CleanupTitle(movieInfo);
        }

        if (!movieInfo.ReleaseDate.HasValue && !movieInfo.HasExternalId)
        {
          // When searching movie title, the year can be relevant for multiple titles with same name but different years
          DateTime recordingDate;
          if (MediaItemAspect.TryGetAttribute(extractedAspectData, MediaAspect.ATTR_RECORDINGTIME, out recordingDate))
            movieInfo.ReleaseDate = recordingDate;
        }

        try
        {
          await MatroskaMatcher.ExtractFromTagsAsync(lfsra, movieInfo).ConfigureAwait(false);
          MP4Matcher.ExtractFromTags(lfsra, movieInfo);
        }
        catch (Exception ex)
        {
          ServiceRegistration.Get<ILogger>().Debug("MoviesMetadataExtractor: Exception reading tags for '{0}'", ex, lfsra.CanonicalLocalResourcePath);
        }
      }

      // Allow the online lookup to choose best matching language for metadata
      if (movieInfo.Languages.Count == 0)
      {
        IList<MultipleMediaItemAspect> audioAspects;
        if (MediaItemAspect.TryGetAspects(extractedAspectData, VideoAudioStreamAspect.Metadata, out audioAspects))
        {
          foreach (MultipleMediaItemAspect aspect in audioAspects)
          {
            string language = (string)aspect.GetAttributeValue(VideoAudioStreamAspect.ATTR_AUDIOLANGUAGE);
            if (!string.IsNullOrEmpty(language) && !movieInfo.Languages.Contains(language))
              movieInfo.Languages.Add(language);
          }
        }
      }

      if (SkipOnlineSearches && !SkipFanArtDownload)
      {
        MovieInfo tempInfo = movieInfo.Clone();
        if (await OnlineMatcherService.Instance.FindAndUpdateMovieAsync(tempInfo).ConfigureAwait(false))
        {
          movieInfo.CopyIdsFrom(tempInfo);
          movieInfo.HasChanged = tempInfo.HasChanged;
        }
      }
      else if (!SkipOnlineSearches)
      {
        await OnlineMatcherService.Instance.FindAndUpdateMovieAsync(movieInfo).ConfigureAwait(false);
      }

      //Send it to the videos section
      if (!SkipOnlineSearches && !movieInfo.HasExternalId)
        return false;

      //Create custom collection (overrides online collection)
      MovieCollectionInfo collectionInfo = movieInfo.CloneBasicInstance<MovieCollectionInfo>();
      string collectionName;
      if (string.IsNullOrEmpty(collectionInfo.NameId) && CollectionFolderHasFanArt(lfsra, out collectionName))
      {
        collectionInfo = new MovieCollectionInfo();
        collectionInfo.CollectionName = collectionName;
        if (!collectionInfo.CollectionName.IsEmpty)
        {
          movieInfo.CollectionName = collectionInfo.CollectionName;
          movieInfo.CopyIdsFrom(collectionInfo); //Reset ID's
          movieInfo.HasChanged = true;
        }
      }

      if (movieInfo.MovieNameSort.IsEmpty)
      {
        if (!movieInfo.CollectionName.IsEmpty && movieInfo.ReleaseDate.HasValue)
          movieInfo.MovieNameSort = $"{movieInfo.CollectionName.Text} {movieInfo.ReleaseDate.Value.Year}-{movieInfo.ReleaseDate.Value.Month.ToString("00")}";
        else if (!movieInfo.MovieName.IsEmpty)
          movieInfo.MovieNameSort = BaseInfo.GetSortTitle(movieInfo.MovieName.Text);
        else
          movieInfo.MovieNameSort = BaseInfo.GetSortTitle(title);
      }
      movieInfo.SetMetadata(extractedAspectData);

      return movieInfo.IsBaseInfoPresent;
    }

    private bool CollectionFolderHasFanArt(ILocalFsResourceAccessor lfsra, out string collectionName)
    {
      collectionName = null;

      // File based access
      try
      {
        using (lfsra.EnsureLocalFileSystemAccess())
        {
          string collectionMediaItemDirectoryPath;
          if (Directory.GetParent(lfsra.LocalFileSystemPath) != null && Directory.GetParent(Directory.GetParent(lfsra.LocalFileSystemPath).FullName) != null)
          {
            DirectoryInfo dir = Directory.GetParent(Directory.GetParent(lfsra.LocalFileSystemPath).FullName);
            collectionMediaItemDirectoryPath = dir.FullName;
            collectionName = dir.Name;
          }
          else
            return false;

          var potentialFanArtFiles = GetPotentialFanArtFiles(collectionMediaItemDirectoryPath);

          if ((from potentialFanArtFile in potentialFanArtFiles
               let potentialFanArtFileNameWithoutExtension = Path.GetFileNameWithoutExtension(potentialFanArtFile.ToString()).ToLowerInvariant()
               where potentialFanArtFileNameWithoutExtension == "poster" || potentialFanArtFileNameWithoutExtension == "folder" || potentialFanArtFileNameWithoutExtension == "movieset-poster"
               select potentialFanArtFile).Count() > 0)
            return true;

          if ((from potentialFanArtFile in potentialFanArtFiles
               let potentialFanArtFileNameWithoutExtension = Path.GetFileNameWithoutExtension(potentialFanArtFile.ToString()).ToLowerInvariant()
               where potentialFanArtFileNameWithoutExtension == "banner" || potentialFanArtFileNameWithoutExtension == "movieset-banner"
               select potentialFanArtFile).Count() > 0)
            return true;

          if ((from potentialFanArtFile in potentialFanArtFiles
               let potentialFanArtFileNameWithoutExtension = Path.GetFileNameWithoutExtension(potentialFanArtFile.ToString()).ToLowerInvariant()
               where potentialFanArtFileNameWithoutExtension == "backdrop" || potentialFanArtFileNameWithoutExtension == "fanart" || potentialFanArtFileNameWithoutExtension == "movieset-fanart"
               select potentialFanArtFile).Count() > 0)
            return true;

          string fanArtFolder = Path.Combine(collectionMediaItemDirectoryPath, "ExtraFanArt");
          if (Directory.Exists(fanArtFolder))
            if (GetPotentialFanArtFiles(fanArtFolder).Count() > 0)
              return true;
        }
      }
      catch
      {
      }
      return false;
    }

    private List<string> GetPotentialFanArtFiles(string folderName)
    {
      var result = new List<string>();
      if (!Directory.Exists(folderName))
        return result;
      foreach (var file in Directory.GetFiles(folderName))
        if (IMG_EXTENSIONS.Contains(Path.GetExtension(file)))
          result.Add(file);
      return result;
    }

    #endregion

    #region IMetadataExtractor implementation

    public MetadataExtractorMetadata Metadata
    {
      get { return _metadata; }
    }

    public async Task<bool> TryExtractMetadataAsync(IResourceAccessor mediaItemAccessor, IDictionary<Guid, IList<MediaItemAspect>> extractedAspectData, bool forceQuickMode)
    {
      try
      {
        if (forceQuickMode)
          return false;

        if (!(mediaItemAccessor is IFileSystemResourceAccessor))
          return false;
        using (LocalFsResourceAccessorHelper rah = new LocalFsResourceAccessorHelper(mediaItemAccessor))
          return await ExtractMovieData(rah.LocalFsResourceAccessor, extractedAspectData).ConfigureAwait(false);
      }
      catch (Exception e)
      {
        // Only log at the info level here - And simply return false. This lets the caller know that we
        // couldn't perform our task here.
        ServiceRegistration.Get<ILogger>().Info("MoviesMetadataExtractor: Exception reading resource '{0}' (Text: '{1}')", mediaItemAccessor.CanonicalLocalResourcePath, e.Message);
      }
      return false;
    }

    public bool IsDirectorySingleResource(IResourceAccessor mediaItemAccessor)
    {
      return false;
    }

    public bool IsStubResource(IResourceAccessor mediaItemAccessor)
    {
      return false;
    }

    public bool TryExtractStubItems(IResourceAccessor mediaItemAccessor, ICollection<IDictionary<Guid, IList<MediaItemAspect>>> extractedStubAspectData)
    {
      return false;
    }

    #endregion
  }
}
