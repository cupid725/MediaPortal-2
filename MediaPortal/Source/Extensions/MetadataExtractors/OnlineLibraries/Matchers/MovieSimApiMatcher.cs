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
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Common.PathManager;
using MediaPortal.Extensions.OnlineLibraries.Wrappers;
using System;
using System.Threading.Tasks;

namespace MediaPortal.Extensions.OnlineLibraries.Matchers
{
  public class MovieSimApiMatcher : MovieMatcher<string, string>
  {
    #region Static instance

    public static MovieSimApiMatcher Instance
    {
      get { return ServiceRegistration.Get<MovieSimApiMatcher>(); }
    }

    #endregion

    #region Constants

    public static string CACHE_PATH = ServiceRegistration.Get<IPathManager>().GetPath(@"<DATA>\SimApi\");
    protected static TimeSpan MAX_MEMCACHE_DURATION = TimeSpan.FromMinutes(10);

    #endregion

    #region Init

    public MovieSimApiMatcher() : 
      base(CACHE_PATH, MAX_MEMCACHE_DURATION, false)
    {
      //TODO: Disabled for now. Very slow and timeouts at times.
      Enabled = false;
    }

    public override Task<bool> InitWrapperAsync(bool useHttps)
    {
      try
      {
        SimApiWrapper wrapper = new SimApiWrapper();
        if (wrapper.Init(CACHE_PATH, useHttps))
        {
          _wrapper = wrapper;
          return Task.FromResult(true);
        }
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error(Id + ": Error initializing wrapper", ex);
      }
      return Task.FromResult(false);
    }

    #endregion

    #region Metadata updaters

    public override Task<bool> FindAndUpdateMovieAsync(MovieInfo movieInfo, bool importOnly)
    {
      // Don't allow SimApi during first import cycle because it is english only
      // If it was allowed it would prevent the update of metadata with preferred language
      // during refresh cycle that also allows searches which might be needed to find metadata 
      // in the preferred language
      if (importOnly && !Primary)
        return Task.FromResult(false);

      return base.FindAndUpdateMovieAsync(movieInfo, importOnly);
    }

    public override Task<bool> UpdateCharactersAsync(MovieInfo movieInfo, bool importOnly)
    {
      return Task.FromResult(false);
    }

    public override Task<bool> UpdateCompaniesAsync(MovieInfo movieInfo, string companyType, bool importOnly)
    {
      return Task.FromResult(false);
    }

    public override Task<bool> UpdateCollectionAsync(MovieCollectionInfo movieCollectionInfo, bool updateMovieList, bool importOnly)
    {
      return Task.FromResult(false);
    }

    #endregion

    #region External match storage

    public override void StoreCharacterMatch(CharacterInfo character)
    {
    }

    public override void StoreCompanyMatch(CompanyInfo company)
    {
    }

    #endregion

    #region Translators

    protected override bool SetMovieId(MovieInfo movie, string id)
    {
      if (!string.IsNullOrEmpty(id))
      {
        movie.ImdbId = id;
        return true;
      }
      return false;
    }

    protected override bool GetMovieId(MovieInfo movie, out string id)
    {
      id = null;
      if (!string.IsNullOrEmpty(movie.ImdbId))
        id = movie.ImdbId;
      return id != null;
    }

    protected override bool GetPersonId(PersonInfo person, out string id)
    {
      id = null;
      if (!string.IsNullOrEmpty(person.ImdbId))
        id = person.ImdbId;
      return id != null;
    }

    protected override bool SetPersonId(PersonInfo person, string id)
    {
      if (!string.IsNullOrEmpty(id))
      {
        person.ImdbId = id;
        return true;
      }
      return false;
    }

    #endregion

    #region FanArt

    protected override bool VerifyFanArtImage(string imageUrl, string language)
    {
      return !string.IsNullOrEmpty(imageUrl);
    }

    #endregion
  }
}
