// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.

namespace Cogs.Publishers.Python;

/// <summary>
/// Specifies the target Python library flavor for generated model packages.
/// </summary>
public enum PythonFlavor
{
    /// <summary>
    /// Standard library Python dataclasses with zero third-party dependencies (Python 3.11+).
    /// </summary>
    Dataclass,

    /// <summary>
    /// Pydantic v2 models (pydantic>=2.0).
    /// </summary>
    Pydantic
}
