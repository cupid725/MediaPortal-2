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

using System.Collections.Generic;
using System.IO;
using System.Web;
using MediaPortal.Extensions.OnlineLibraries.Libraries.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common;
using MediaPortal.Extensions.OnlineLibraries.Libraries.SimApiV1.Data;

namespace MediaPortal.Extensions.OnlineLibraries.Libraries.SimApiV1
{
  internal class SimApiV1
  {
    #region Constants

    public const string DefaultLanguage = "en";

    private const string URL_API_BASE = "moviesapi.com/m.php";
    private const string URL_QUERYMOVIE = URL_API_BASE + "?t={0}&y={1}&type=movie";
    private const string URL_GETIMDBIDMOVIE =   URL_API_BASE + "?i={0}&type=movie";
    private const string URL_QUERYPERSON = URL_API_BASE + "t={0}&y=&type=person";
    private const string URL_GETIMDBIDPERSON = URL_API_BASE + "?i={0}&type=person";

    #endregion

    #region Fields

    private readonly string _cachePath;
    private readonly Downloader _downloader;
    private object _movieSync = new object();
    private object _personSync = new object();
    private readonly bool _useHttps;

    #endregion

    #region Constructor

    public SimApiV1(string cachePath, bool useHttps)
    {
      _cachePath = cachePath;
      _useHttps = true;
      _downloader = new Downloader { EnableCompression = true };
      _downloader.Headers["Accept"] = "application/json";
    }

    #endregion

    #region Public members

    /// <summary>
    /// Search for movies by name given in <paramref name="title"/>.
    /// </summary>
    /// <param name="title">Full or partly name of movie</param>
    /// <returns>List of possible matches</returns>
    public List<SimApiMovieSearchItem> SearchMovie(string title, int year)
    {
      string url = GetUrl(URL_QUERYMOVIE, HttpUtility.UrlEncode(title), year > 0 ? year.ToString() : "");
      SimApiMovieSearchResult results = _downloader.Download<SimApiMovieSearchResult>(url);
      return results.SearchResults;
    }

    /// <summary>
    /// Search for person by name given in <paramref name="name"/>.
    /// </summary>
    /// <param name="name">Full or partly name of person</param>
    /// <returns>List of possible matches</returns>
    public List<SimApiPersonSearchItem> SearchPerson(string name)
    {
      string url = GetUrl(URL_QUERYPERSON, HttpUtility.UrlEncode(name));
      SimApiPersonSearchResult results = _downloader.Download<SimApiPersonSearchResult>(url);
      return results.SearchResults;
    }

    /// <summary>
    /// Returns detailed information for a single <see cref="SimApiMovie"/> with given <paramref name="id"/>. This method caches request
    /// to same movies using the cache path given in <see cref="SimApiV1"/> constructor.
    /// </summary>
    /// <param name="id">IMDB id of movie</param>
    /// <returns>Movie information</returns>
    public SimApiMovie GetMovie(string id, bool cacheOnly)
    {
      lock (_movieSync)
      {
        string cache = CreateAndGetCacheName(id, "Movie");
        SimApiMovie returnValue = null;
        if (!string.IsNullOrEmpty(cache) && File.Exists(cache))
        {
          returnValue = _downloader.ReadCache<SimApiMovie>(cache);
        }
        else
        {
          if (cacheOnly) return null;
          string url = GetUrl(URL_GETIMDBIDMOVIE, id.StartsWith("tt", System.StringComparison.InvariantCultureIgnoreCase) ? id.Substring(2) : id);
          returnValue = _downloader.Download<SimApiMovie>(url, cache);
        }
        if (returnValue == null) return null;
        return returnValue;
      }
    }

    /// <summary>
    /// Returns cache file for a single <see cref="SimApiMovie"/> with given <paramref name="id"/>.
    /// </summary>
    /// <param name="id">IMDB id of movie</param>
    /// <returns>Cache file name</returns>
    public string GetMovieCacheFile(string id)
    {
      return CreateAndGetCacheName(id, "Movie");
    }

    /// <summary>
    /// Returns detailed information for a single <see cref="SimApiPerson"/> with given <paramref name="id"/>. This method caches request
    /// to same person using the cache path given in <see cref="SimApiV1"/> constructor.
    /// </summary>
    /// <param name="id">IMDB id of series</param>
    /// <returns>Person information</returns>
    public SimApiPerson GetPerson(string id, bool cacheOnly)
    {
      lock (_personSync)
      {
        string cache = CreateAndGetCacheName(id, "Person");
        SimApiPerson returnValue = null;
        if (!string.IsNullOrEmpty(cache) && File.Exists(cache))
        {
          returnValue = _downloader.ReadCache<SimApiPerson>(cache);
        }
        else
        {
          if (cacheOnly) return null;
          string url = GetUrl(URL_GETIMDBIDPERSON, id.StartsWith("nm", System.StringComparison.InvariantCultureIgnoreCase) ? id.Substring(2) : id);
          returnValue = _downloader.Download<SimApiPerson>(url, cache);
        }
        if (returnValue == null) return null;
        return returnValue;
      }
    }

    /// <summary>
    /// Returns cache file for a single <see cref="SimApiPerson"/> with given <paramref name="id"/>.
    /// </summary>
    /// <param name="id">IMDB id of series</param>
    /// <returns>Cache file name</returns>
    public string GetSeriesCacheFile(string id)
    {
      return CreateAndGetCacheName(id, "Person");
    }

    /// <summary>
    /// Downloads images and saves them to cache.
    /// </summary>
    /// <param name="image">Image to download</param>
    /// <param name="folderPath">The folder to store the image</param>
    /// <returns><c>true</c> if successful</returns>
    public bool DownloadImage(string Id, string imageUrl, string folderPath)
    {
      string cacheFileName = CreateAndGetCacheName(Id, imageUrl, folderPath);
      if (string.IsNullOrEmpty(cacheFileName))
        return false;

      _downloader.DownloadFile(imageUrl, cacheFileName);
      return true;
    }

    #endregion

    #region Protected members

    /// <summary>
    /// Builds and returns the full request url.
    /// </summary>
    /// <param name="urlBase">Query base</param>
    /// <param name="args">Optional arguments to format <paramref name="urlBase"/></param>
    /// <returns>Complete url</returns>
    protected string GetUrl(string urlBase, params object[] args)
    {
      string replacedUrl = string.Format(urlBase, args);

      if(_useHttps)
        return string.Format("https://{0}&r=json", replacedUrl);
      else
        return string.Format("http://{0}&r=json", replacedUrl);
    }

    /// <summary>
    /// Creates a local file name for loading and saving details for movie. It supports both TMDB id and IMDB id.
    /// </summary>
    /// <param name="movieId"></param>
    /// <param name="prefix"></param>
    /// <returns>Cache file name or <c>null</c> if directory could not be created</returns>
    protected string CreateAndGetCacheName<TE>(TE movieId, string prefix)
    {
      try
      {
        string folder = Path.Combine(_cachePath, movieId.ToString());
        if (!Directory.Exists(folder))
          Directory.CreateDirectory(folder);
        return Path.Combine(folder, string.Format("{0}.json", prefix));
      }
      catch
      {
        // TODO: logging
        return null;
      }
    }

    /// <summary>
    /// Creates a local file name for loading and saving images.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="folderPath"></param>
    /// <returns>Cache file name or <c>null</c> if directory could not be created</returns>
    protected string CreateAndGetCacheName(string id, string imageUrl, string folderPath)
    {
      try
      {
        string prefix = string.Format(@"SIM({0})_", id);
        if (!Directory.Exists(folderPath))
          Directory.CreateDirectory(folderPath);
        return Path.Combine(folderPath, prefix + imageUrl.Substring(imageUrl.LastIndexOf('/') + 1));
      }
      catch
      {
        // TODO: logging
        return null;
      }
    }

    protected static ILogger Logger
    {
      get
      {
        return ServiceRegistration.Get<ILogger>();
      }
    }

    #endregion
  }
}