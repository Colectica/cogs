// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.

namespace Cogs.Dto
{
    /// <summary>Retains the source location of a line-oriented convention entry.</summary>
    public sealed class SourceTextEntry
    {
        public string Value { get; set; }
        public string SourcePath { get; set; }
        public int? SourceLine { get; set; }
        public int? SourceColumn { get; set; }
    }
}
