﻿#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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
using System.Collections.Generic;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Common.General;
using MediaPortal.Extensions.OnlineLibraries;
using MediaPortal.Common.MediaManagement.MLQueries;

namespace MediaPortal.Extensions.MetadataExtractors.MovieMetadataExtractor
{
  class MovieCollectionRelationshipExtractor : IMovieRelationshipExtractor, IRelationshipRoleExtractor
  {
    private static readonly Guid[] ROLE_ASPECTS = { MovieAspect.ASPECT_ID };
    private static readonly Guid[] LINKED_ROLE_ASPECTS = { MovieCollectionAspect.ASPECT_ID };
    private CheckedItemCache<MovieInfo> _checkCache = new CheckedItemCache<MovieInfo>(MovieMetadataExtractor.MINIMUM_HOUR_AGE_BEFORE_UPDATE);
    private CheckedItemCache<MovieCollectionInfo> _collectionCache = new CheckedItemCache<MovieCollectionInfo>(MovieMetadataExtractor.MINIMUM_HOUR_AGE_BEFORE_UPDATE);

    public bool BuildRelationship
    {
      get { return true; }
    }

    public Guid Role
    {
      get { return MovieAspect.ROLE_MOVIE; }
    }

    public Guid[] RoleAspects
    {
      get { return ROLE_ASPECTS; }
    }

    public Guid LinkedRole
    {
      get { return MovieCollectionAspect.ROLE_MOVIE_COLLECTION; }
    }

    public Guid[] LinkedRoleAspects
    {
      get { return LINKED_ROLE_ASPECTS; }
    }

    public IFilter[] GetSearchFilters(IDictionary<Guid, IList<MediaItemAspect>> extractedAspects)
    {
      return GetMovieCollectionSearchFilters(extractedAspects);
    }

    public bool TryExtractRelationships(IDictionary<Guid, IList<MediaItemAspect>> aspects, out ICollection<IDictionary<Guid, IList<MediaItemAspect>>> extractedLinkedAspects, bool forceQuickMode)
    {
      extractedLinkedAspects = null;

      MovieInfo movieInfo = new MovieInfo();
      if (!movieInfo.FromMetadata(aspects))
        return false;

      if (_checkCache.IsItemChecked(movieInfo))
        return false;

      MovieCollectionInfo collectionInfo;
      if (!_collectionCache.TryGetCheckedItem(movieInfo.CloneBasicInstance<MovieCollectionInfo>(), out collectionInfo))
      {
        collectionInfo = movieInfo.CloneBasicInstance<MovieCollectionInfo>();
        if (!MovieMetadataExtractor.SkipOnlineSearches)
          OnlineMatcherService.Instance.UpdateCollection(collectionInfo, false, false);
        collectionInfo.HasChanged = false; //Reset change status on cached instance
        _collectionCache.TryAddCheckedItem(collectionInfo);
      }

      if (!BaseInfo.HasRelationship(aspects, LinkedRole))
        collectionInfo.HasChanged = true; //Force save if no relationship exists

      if (!collectionInfo.HasChanged && !forceQuickMode)
        return false;

      extractedLinkedAspects = new List<IDictionary<Guid, IList<MediaItemAspect>>>();
      IDictionary<Guid, IList<MediaItemAspect>> collectionAspects = new Dictionary<Guid, IList<MediaItemAspect>>();
      collectionInfo.SetMetadata(collectionAspects);

      bool movieVirtual = true;
      if (MediaItemAspect.TryGetAttribute(aspects, MediaAspect.ATTR_ISVIRTUAL, false, out movieVirtual))
      {
        MediaItemAspect.SetAttribute(collectionAspects, MediaAspect.ATTR_ISVIRTUAL, movieVirtual);
      }

      if (collectionAspects.ContainsKey(ExternalIdentifierAspect.ASPECT_ID))
        extractedLinkedAspects.Add(collectionAspects);

      //Create custom collection
      if (!string.IsNullOrEmpty(movieInfo.CollectionNameId))
      {
        MovieCollectionInfo customCollectionInfo = movieInfo.CloneBasicInstance<MovieCollectionInfo>();

        IDictionary<Guid, IList<MediaItemAspect>> customCollectionAspects = new Dictionary<Guid, IList<MediaItemAspect>>();
        customCollectionInfo.HasChanged = true;
        customCollectionInfo.SetMetadata(customCollectionAspects);

        if (customCollectionAspects.ContainsKey(ExternalIdentifierAspect.ASPECT_ID))
          extractedLinkedAspects.Add(customCollectionAspects);
      }

      return extractedLinkedAspects.Count > 0;
    }

    public bool TryMatch(IDictionary<Guid, IList<MediaItemAspect>> extractedAspects, IDictionary<Guid, IList<MediaItemAspect>> existingAspects)
    {
      return existingAspects.ContainsKey(MovieCollectionAspect.ASPECT_ID);
    }

    public bool TryGetRelationshipIndex(IDictionary<Guid, IList<MediaItemAspect>> aspects, IDictionary<Guid, IList<MediaItemAspect>> linkedAspects, out int index)
    {
      index = -1;

      MovieCollectionInfo collectionInfo = new MovieCollectionInfo();
      if (!collectionInfo.FromMetadata(linkedAspects))
        return false;

      if (!OnlineMatcherService.Instance.UpdateCollection(collectionInfo, true, true))
        return false;

      MovieInfo movieInfo = new MovieInfo();
      if (!movieInfo.FromMetadata(aspects))
        return false;

      foreach(MovieInfo movie in collectionInfo.Movies)
      {
        if (movie.MovieDbId == movieInfo.MovieDbId)
        {
          index = movie.Order;
          break;
        }
      }
      return index >= 0;
    }

    public override void ClearCache()
    {
      _checkCache.ClearCache();
      _collectionCache.ClearCache();
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}
