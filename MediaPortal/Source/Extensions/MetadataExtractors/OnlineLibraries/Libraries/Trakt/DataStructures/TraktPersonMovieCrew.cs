﻿#region Copyright (C) 2007-2017 Team MediaPortal

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
using System.Runtime.Serialization;

namespace MediaPortal.Extensions.OnlineLibraries.Libraries.Trakt.DataStructures
{
  [DataContract]
  public class TraktPersonMovieCrew
  {
    [DataMember(Name = "directing")]
    public List<TraktPersonMovieJob> Directing { get; set; }

    [DataMember(Name = "writing")]
    public List<TraktPersonMovieJob> Writing { get; set; }

    [DataMember(Name = "production")]
    public List<TraktPersonMovieJob> Production { get; set; }

    [DataMember(Name = "art")]
    public List<TraktPersonMovieJob> Art { get; set; }

    [DataMember(Name = "costume & make-up")]
    public List<TraktPersonMovieJob> CostumeAndMakeUp { get; set; }

    [DataMember(Name = "sound")]
    public List<TraktPersonMovieJob> Sound { get; set; }

    [DataMember(Name = "camera")]
    public List<TraktPersonMovieJob> Camera { get; set; }

    [DataMember(Name = "crew")]
    public List<TraktPersonMovieJob> Crew { get; set; }
  }
}