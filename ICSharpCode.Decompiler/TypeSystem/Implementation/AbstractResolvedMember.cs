﻿// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.TypeSystem.Implementation
{
	/// <summary>
	/// Implementation of <see cref="IMember"/> that resolves an unresolved member.
	/// </summary>
	public abstract class AbstractResolvedMember : AbstractResolvedEntity, IMember
	{
		protected new readonly IUnresolvedMember unresolved;
		protected readonly ITypeResolveContext context;
		volatile IType returnType;
		IReadOnlyList<IMember> implementedInterfaceMembers;
		
		protected AbstractResolvedMember(IUnresolvedMember unresolved, ITypeResolveContext parentContext)
			: base(unresolved, parentContext)
		{
			this.unresolved = unresolved;
			this.context = parentContext.WithCurrentMember(this);
		}
		
		IMember IMember.MemberDefinition => this;

		public IType ReturnType => this.returnType ?? (this.returnType = unresolved.ReturnType.Resolve(context));

		public IUnresolvedMember UnresolvedMember => unresolved;

		public IReadOnlyList<IMember> ImplementedInterfaceMembers {
			get {
				var result = LazyInit.VolatileRead(ref this.implementedInterfaceMembers);
				if (result != null) {
					return result;
				} else {
					return LazyInit.GetOrSet(ref implementedInterfaceMembers, FindImplementedInterfaceMembers());
				}
			}
		}

		IReadOnlyList<IMember> FindImplementedInterfaceMembers()
		{
			if (unresolved.IsExplicitInterfaceImplementation) {
				var result = new List<IMember>();
				foreach (var memberReference in unresolved.ExplicitInterfaceImplementations) {
					var member = memberReference.Resolve(context);
					if (member != null)
						result.Add(member);
				}
				return result.ToArray();
			} else if (unresolved.IsStatic || !unresolved.IsPublic || DeclaringTypeDefinition == null || DeclaringTypeDefinition.Kind == TypeKind.Interface) {
				return EmptyList<IMember>.Instance;
			} else {
				// TODO: implement interface member mappings correctly
				var result = InheritanceHelper.GetBaseMembers(this, true)
					.Where(m => m.DeclaringTypeDefinition != null && m.DeclaringTypeDefinition.Kind == TypeKind.Interface)
					.ToArray();

				IEnumerable<IMember> otherMembers = DeclaringTypeDefinition.Members;
				if (SymbolKind == SymbolKind.Accessor)
					otherMembers = DeclaringTypeDefinition.GetAccessors(options: GetMemberOptions.IgnoreInheritedMembers);
				result = result.Where(item => !otherMembers.Any(m => m.IsExplicitInterfaceImplementation && m.ImplementedInterfaceMembers.Contains(item))).ToArray();

				return result;
			}
		}
		
		public bool IsExplicitInterfaceImplementation => unresolved.IsExplicitInterfaceImplementation;

		public bool IsVirtual => unresolved.IsVirtual;

		public bool IsOverride => unresolved.IsOverride;

		public bool IsOverridable => unresolved.IsOverridable;

		public TypeParameterSubstitution Substitution => TypeParameterSubstitution.Identity;

		public abstract IMember Specialize(TypeParameterSubstitution substitution);
		
		IMemberReference IMember.ToReference()
		{
			return (IMemberReference)ToReference();
		}
		
		public override ISymbolReference ToReference()
		{
			var declType = this.DeclaringType;
			var declTypeRef = declType != null ? declType.ToTypeReference() : SpecialType.UnknownType;
			if (IsExplicitInterfaceImplementation && ImplementedInterfaceMembers.Count == 1) {
				return new ExplicitInterfaceImplementationMemberReference(declTypeRef, ImplementedInterfaceMembers[0].ToReference());
			} else {
				return new DefaultMemberReference(this.SymbolKind, declTypeRef, this.Name);
			}
		}
		
		internal IMethod GetAccessor(ref IMethod accessorField, IUnresolvedMethod unresolvedAccessor)
		{
			if (unresolvedAccessor == null)
				return null;
			var result = LazyInit.VolatileRead(ref accessorField);
			if (result != null) {
				return result;
			} else {
				return LazyInit.GetOrSet(ref accessorField, CreateResolvedAccessor(unresolvedAccessor));
			}
		}
		
		protected virtual IMethod CreateResolvedAccessor(IUnresolvedMethod unresolvedAccessor)
		{
			return (IMethod)unresolvedAccessor.CreateResolved(context);
		}
	}
}
