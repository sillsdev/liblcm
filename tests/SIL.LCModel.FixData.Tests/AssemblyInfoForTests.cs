// // Copyright (c) 2018 SIL International
// // This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using SIL.LCModel.Core.Attributes;
using SIL.LCModel.Utils.Attributes;
using SIL.TestUtilities;

[assembly: CleanupSingletons]
[assembly: InitializeIcu(IcuDataPath = "IcuData")]
[assembly: OfflineSldr]
