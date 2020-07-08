// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Reflection;
using SIL.LCModel.Core.Attributes;
using SIL.LCModel.Utils.Attributes;
using SIL.TestUtilities;

//Cleanup all singletons after running tests
[assembly: CleanupSingletons]
[assembly: InitializeIcu(IcuDataPath = "IcuData")]
[assembly: OfflineSldr]
