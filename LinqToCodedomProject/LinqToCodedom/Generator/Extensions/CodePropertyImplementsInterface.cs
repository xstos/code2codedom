﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.CodeDom;
using LinqToCodedom.CustomCodeDomGeneration;
using LinqToCodedom.Extensions;
using LinqToCodedom.Generator;
using System.Linq;

namespace LinqToCodedom.CodeDomPatterns
{

    public class CodePropertyImplementsInterface : CodeSnippetTypeMember, ICustomCodeDomObject
    {
        public IList<Tuple<CodeTypeReference, String>> InterfaceProperties { get; set; }

        public CodePropertyImplementsInterface(CodeMemberProperty property)
        {
            InterfaceProperties = new List<Tuple<CodeTypeReference, string>>();
            _property = property;
        }

        //public CodePropertyImplementsInterface(CodeMemberProperty property,
        //    CodeParameterDeclarationExpressionCollection parameters)
        //{
        //    Parameters = parameters;
        //    _property = property;
        //}

        private CodeMemberProperty _property;

        public CodeMemberProperty Property
        {
            get
            {
                return _property;
            }
            set
            {
                _property = value;
            }
        }

        #region ICustomCodeDomObject Members

        public void GenerateCode(CodeDomGenerator.Language language)
        {
            if (_property == null)
                return;

            switch (language)
            {
                case CodeDomGenerator.Language.CSharp:
                    GenerateCS();
                    break;
                case CodeDomGenerator.Language.VB:
                    GenerateVB();
                    break;
                default:
                    throw new NotImplementedException(language.ToString());
            }
        }

        #region "VB Code"

        private void GenerateVB()
        {
            using (Microsoft.VisualBasic.VBCodeProvider provider = new Microsoft.VisualBasic.VBCodeProvider())
            {
                CodeGeneratorOptions opts = new CodeGeneratorOptions();
                StringWriter sw = new StringWriter();
                List<CodeTypeReference> implTypes = new List<CodeTypeReference>();
                if (_property.ImplementationTypes != null)
                {
                    implTypes.AddRange(_property.ImplementationTypes.Cast<CodeTypeReference>().Distinct(new CodeTypeReferenceEqualityComparer()));
                    _property.ImplementationTypes.Clear();
                }
                provider.GenerateCodeFromMember(_property, sw, opts);
                foreach (CodeTypeReference tr in implTypes)
                {
                    _property.ImplementationTypes.Add(tr);
                }
                StringReader sr = new StringReader(sw.GetStringBuilder().ToString());
                string line = sr.ReadLine();
                while (string.IsNullOrEmpty(line) || line.StartsWith("'") || line.StartsWith("<"))
                    line = sr.ReadLine();

                StringBuilder sb = new StringBuilder();
                sb.Append(line);
                if (InterfaceProperties != null)
                {
                    sb.Append(" Implements ");
                    foreach (CodeTypeReference tr in implTypes)
                    {
                        foreach(var prop in InterfaceProperties.Where(it => it.Item1.IsEquals(tr)))
                        {
                            sb.Append(provider.GetTypeOutput(tr)).Append(".").Append(prop.Item2).Append(", ");
                        }
                    }
                    sb.Length -= 2;
                }
                Text = sw.GetStringBuilder().Replace(line, sb.ToString()).ToString();
            }
        }

        #endregion

        #region "CS Code"

        private void GenerateCS()
        {
            using (Microsoft.CSharp.CSharpCodeProvider provider = new Microsoft.CSharp.CSharpCodeProvider())
            {
                CodeGeneratorOptions opts = new CodeGeneratorOptions();
                StringWriter sw = new StringWriter();
                List<CodeTypeReference> implTypes = new List<CodeTypeReference>();
                if (_property.ImplementationTypes != null)
                {
                    implTypes.AddRange(_property.ImplementationTypes.Cast<CodeTypeReference>().Distinct(new CodeTypeReferenceEqualityComparer()));
                    _property.ImplementationTypes.Clear();
                }
                provider.GenerateCodeFromMember(_property, sw, opts);
                foreach (CodeTypeReference tr in implTypes)
                {
                    _property.ImplementationTypes.Add(tr);
                }
                //StringReader sr = new StringReader(sw.GetStringBuilder().ToString());
                //string line = sr.ReadLine();
                //while (string.IsNullOrEmpty(line) || line.StartsWith("/") || line.StartsWith("["))
                //    line = sr.ReadLine();

                StringBuilder sb = new StringBuilder();

                if (InterfaceProperties != null)
                {
                    foreach (CodeTypeReference tr in implTypes)
                    {
                        foreach (var prop in InterfaceProperties.Where(it => it.Item1.IsEquals(tr)))
                        {
                            var newProp = Define.Property(_property.Type, MemberAttributes.Private, prop.Item2).Implements(tr);
                            if (_property.HasGet)
                            {
                                newProp.GetStatements.Add(Emit.@return(()=>CodeDom.@this.Property(_property.Name)));
                                newProp.HasGet = true;
                            }
                            if (_property.HasSet)
                            {
                                newProp.SetStatements.Add(Emit.assignProperty(_property.Name, () => CodeDom.VarRef("value")));
                                newProp.HasSet = true;
                            }

                            StringWriter swNew = new StringWriter();
                            provider.GenerateCodeFromMember(CodeDomTreeProcessor.ProcessMember(newProp, CodeDomGenerator.Language.CSharp), 
                                swNew, opts);
                            sb.Append(swNew.ToString());
                        }
                    }
                    if (sb.Length > 0)
                        sb.Insert(0, Environment.NewLine);
                }
                Text = sw.GetStringBuilder().ToString() + sb.ToString();
            }
        }

        #endregion

        #endregion
    }
}
