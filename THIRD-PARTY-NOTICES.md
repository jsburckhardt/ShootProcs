# Third-party notices

The application uses Microsoft .NET and Windows Desktop runtime components.
The portable package includes the SDK-provided `LICENSE.txt` and
`ThirdPartyNotices.txt` for redistribution notices. Windows itself is a platform
dependency and is not redistributed.

The application dependency graph additionally includes these MIT-licensed
NuGet packages, as verified from their package metadata:

- Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0
- Microsoft.Diagnostics.NETCore.Client 0.2.661903
- Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2
- Microsoft.Extensions.Logging.Abstractions 8.0.3
- System.Reflection.TypeExtensions 4.7.0

Some package APIs are supplied by the target runtime rather than a separate
published assembly. This inventory covers the resolved application dependency
graph, not development-only xUnit/test SDK tooling.

The optional setup executable is built with unmodified
[Inno Setup](https://jrsoftware.org/inno-setup), copyright Jordan Russell and
Martijn Laan, under the [Inno Setup License](https://jrsoftware.org/files/is/license.txt).
Its compiler is a build dependency, not part of the installed application.

## MIT License

Copyright (c) Microsoft Corporation. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to
do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
