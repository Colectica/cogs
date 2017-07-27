// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Publishers;

namespace Cogs.Console
{
    public class OwlPublisher : GraphQLPublisher
    {
        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }
    }
}