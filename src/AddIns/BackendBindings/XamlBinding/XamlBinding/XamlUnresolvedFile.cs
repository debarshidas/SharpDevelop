﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.Core;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.Editor;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Xml;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Parser;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Refactoring;

namespace ICSharpCode.XamlBinding
{
	// TODO: add [Serializable] support for FastSerializer
	// (SD code may require modifications so that addin assemblies
	// can be found by the deserializer)
	public sealed class XamlUnresolvedFile : IUnresolvedFile
	{
		FileName fileName;
		List<Error> errors;
		IUnresolvedTypeDefinition[] topLevel;
		
		XamlUnresolvedFile(FileName fileName)
		{
			this.fileName = fileName;
			this.errors = new List<Error>();
		}
		
		public static XamlUnresolvedFile Create(FileName fileName, ITextSource fileContent, AXmlDocument document)
		{
			XamlUnresolvedFile file = new XamlUnresolvedFile(fileName);
			
			file.errors.AddRange(document.SyntaxErrors.Select(err => new Error(ErrorType.Error, err.Description)));
			var visitor = new XamlDocumentVisitor(file, fileContent);
			visitor.VisitDocument(document);
			if (visitor.TypeDefinition != null)
				file.topLevel = new[] { visitor.TypeDefinition };
			else
				file.topLevel = new IUnresolvedTypeDefinition[0];
			
			return file;
		}
		
		public string FileName {
			get { return fileName; }
		}
		
		DateTime? lastWriteTime;
		
		public DateTime? LastWriteTime {
			get { return lastWriteTime; }
			set { lastWriteTime = value; }
		}
		
		IList<IUnresolvedTypeDefinition> IUnresolvedFile.TopLevelTypeDefinitions {
			get { return topLevel; }
		}
		
		IList<IUnresolvedAttribute> IUnresolvedFile.AssemblyAttributes {
			get { return EmptyList<IUnresolvedAttribute>.Instance; }
		}
		
		IList<IUnresolvedAttribute> IUnresolvedFile.ModuleAttributes {
			get { return EmptyList<IUnresolvedAttribute>.Instance; }
		}
		
		public IList<Error> Errors {
			get { return errors; }
		}
		
		IUnresolvedTypeDefinition IUnresolvedFile.GetTopLevelTypeDefinition(TextLocation location)
		{
			foreach (var td in topLevel) {
				if (td.Region.IsInside(location))
					return td;
			}
			return null;
		}
		
		public IUnresolvedTypeDefinition TypeDefinition {
			get { return topLevel.FirstOrDefault(); }
		}
		
		IUnresolvedTypeDefinition IUnresolvedFile.GetInnermostTypeDefinition(TextLocation location)
		{
			return ((IUnresolvedFile)this).GetTopLevelTypeDefinition(location);
		}
		
		public IUnresolvedMember GetMember(TextLocation location)
		{
			var td = ((IUnresolvedFile)this).GetInnermostTypeDefinition(location);
			if (td != null) {
				foreach (var md in td.Members) {
					if (md.Region.IsInside(location))
						return md;
				}
			}
			return null;
		}
		
		public static ITypeReference CreateTypeReference(string @namespace, string localName)
		{
			if (@namespace.StartsWith("clr-namespace:", StringComparison.OrdinalIgnoreCase)) {
				return CreateClrNamespaceTypeReference(@namespace.Substring("clr-namespace:".Length), localName);
			}
			return new XamlTypeReference(@namespace, localName);
		}
		
		public static ITypeReference CreateClrNamespaceTypeReference(string @namespace, string localName)
		{
			int assemblyNameIndex = @namespace.IndexOf(";assembly=", StringComparison.OrdinalIgnoreCase);
			IAssemblyReference asm = DefaultAssemblyReference.CurrentAssembly;
			if (assemblyNameIndex > -1) {
				asm = new DefaultAssemblyReference(@namespace.Substring(assemblyNameIndex + ";assembly=".Length));
				@namespace = @namespace.Substring(0, assemblyNameIndex);
			}
			return new GetClassTypeReference(asm, @namespace, localName, 0);
		}
		
		class XamlDocumentVisitor : AXmlVisitor
		{
			public DefaultUnresolvedTypeDefinition TypeDefinition { get; private set; }
			
			IUnresolvedFile file;
			AXmlDocument currentDocument;
			ReadOnlyDocument textDocument;
			
			public XamlDocumentVisitor(IUnresolvedFile file, ITextSource fileContent)
			{
				this.file = file;
				textDocument = new ReadOnlyDocument(fileContent, file.FileName);
			}
			
			public override void VisitDocument(AXmlDocument document)
			{
				currentDocument = document;
				AXmlElement rootElement = currentDocument.Children.OfType<AXmlElement>().FirstOrDefault();
				if (rootElement != null) {
					string className = rootElement.GetAttributeValue(XamlConst.XamlNamespace, "Class");
					if (className != null) {
						TypeDefinition = new DefaultUnresolvedTypeDefinition(className) {
							Kind = TypeKind.Class,
							UnresolvedFile = file,
							Region = new DomRegion(file.FileName, textDocument.GetLocation(rootElement.StartOffset), textDocument.GetLocation(rootElement.EndOffset))
						};
						TypeDefinition.Members.Add(
							new DefaultUnresolvedMethod(TypeDefinition, "InitializeComponent") {
								Accessibility = Accessibility.Public,
								ReturnType = KnownTypeReference.Void
							});
					}
				}
				base.VisitDocument(document);
			}
			
			public override void VisitElement(AXmlElement element)
			{
				string name = element.GetAttributeValue(XamlConst.XamlNamespace, "Name") ??
					element.GetAttributeValue("Name");
				string modifier = element.GetAttributeValue(XamlConst.XamlNamespace, "FieldModifier");
				
				if (name != null && TypeDefinition != null) {
					var field = new DefaultUnresolvedField(TypeDefinition, name);
					field.Accessibility = Accessibility.Internal;
					field.ReturnType = CreateTypeReference(element.Namespace, element.LocalName);
					field.Region = new DomRegion(file.FileName, textDocument.GetLocation(element.StartOffset), textDocument.GetLocation(element.EndOffset));
					if (modifier != null)
						field.Accessibility = ParseAccessibility(modifier);
					TypeDefinition.Members.Add(field);
				}
				
				base.VisitElement(element);
			}
			
			Accessibility ParseAccessibility(string value)
			{
				if ("public".Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
					return Accessibility.Public;
				return Accessibility.Internal;
			}
		}
	}
}
