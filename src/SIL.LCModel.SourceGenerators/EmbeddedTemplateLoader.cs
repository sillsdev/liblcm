// Copyright (c) 2025 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Commons.Collections;
using NVelocity.Exception;
using NVelocity.Runtime.Resource;
using NVelocity.Runtime.Resource.Loader;

namespace SIL.LCModel.SourceGenerators
{
	/// <summary>
	/// An NVelocity <see cref="ResourceLoader"/> that serves templates from an in-memory
	/// dictionary keyed by their bare file name (e.g. "class.vm.cs"). This replaces the old
	/// <c>FileResourceLoader</c> + <c>Directory.SetCurrentDirectory</c> mechanism so the engine
	/// can run inside a Roslyn source generator, where templates live as embedded resources and
	/// there is no meaningful current directory.
	/// </summary>
	/// <remarks>
	/// The template set is supplied per engine through the <c>embedded.resource.loader.lcm-templates</c>
	/// property (see <see cref="LcmGenerateImpl"/>), which NVelocity hands to <see cref="Init"/> as
	/// this loader instance's configuration. Nothing here is shared between engines, so concurrent
	/// generations are independent.
	/// </remarks>
	internal sealed class EmbeddedTemplateLoader : ResourceLoader
	{
		public const string TemplatesKey = "lcm-templates";
		/// <summary>The templates to serve, keyed by bare file name (e.g. "main.vm.cs").</summary>
		public IReadOnlyDictionary<string, string> Templates { get; set; }

		public override void Init(ExtendedProperties configuration)
		{
			Templates = configuration.GetProperty(TemplatesKey) as IReadOnlyDictionary<string, string> ?? throw new InvalidOperationException($"No templates supplied (key: {TemplatesKey})");
		}

		public override Stream GetResourceStream(string source)
		{
			// NVelocity may hand us names with directory separators or leading "./"; normalize
			// to the bare file name we key on.
			var name = source.Replace('\\', '/');
			var slash = name.LastIndexOf('/');
			if (slash >= 0)
				name = name.Substring(slash + 1);

			if (Templates != null && Templates.TryGetValue(name, out var text))
				return new MemoryStream(Encoding.UTF8.GetBytes(text));

			throw new ResourceNotFoundException($"EmbeddedTemplateLoader: cannot locate template '{source}'.");
		}

		public override bool IsSourceModified(Resource resource) => false;

		public override long GetLastModified(Resource resource) => 0;
	}
}
