﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

namespace BasaltHexagons.UniversalFileSystem;

public static class UniversalFileSystemFactory
{
    public static IUniversalFileSystem Create(IServiceProvider serviceProvider, IConfiguration configuration)
        => new UniversalFileSystem(new DefaultFileSystemCreator(serviceProvider, configuration));
}
