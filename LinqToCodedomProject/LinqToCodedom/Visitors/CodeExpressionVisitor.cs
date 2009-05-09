﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.CodeDom;
using System.Linq.Expressions;
using LinqToCodedom.Generator;

namespace LinqToCodedom.Visitors
{
    public class CodeExpressionVisitor
    {
        private VisitorContext _ctx;

        public CodeExpressionVisitor(VisitorContext ctx)
        {
            _ctx = ctx;
        }

        public CodeExpression Visit(Expression exp)
        {
            CodeExpression res = _Visit(exp);
            if (res is CodeDom.CodeThisExpression)
                res = new CodeThisReferenceExpression();
            else if (res is CodeDom.CodeBaseExpression)
                res = new CodeBaseReferenceExpression();

            return res;
        }

        private CodeExpression _Visit(Expression exp)
        {
            if (exp == null)
                return null;

            switch (exp.NodeType)
            {
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.ArrayLength:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                    return this.VisitUnary((UnaryExpression)exp);
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.Coalesce:
                case ExpressionType.ArrayIndex:
                case ExpressionType.RightShift:
                case ExpressionType.LeftShift:
                case ExpressionType.ExclusiveOr:
                    return this.VisitBinary((BinaryExpression)exp);
                case ExpressionType.TypeIs:
                //return this.VisitTypeIs((TypeBinaryExpression)exp);
                case ExpressionType.Conditional:
                //return this.VisitConditional((ConditionalExpression)exp);
                case ExpressionType.Constant:
                    return this.VisitConstant((ConstantExpression)exp);
                case ExpressionType.Parameter:
                    return _ctx.VisitParameter((ParameterExpression)exp);
                case ExpressionType.MemberAccess:
                    return this.VisitMemberAccess((MemberExpression)exp);
                case ExpressionType.Call:
                    return this.VisitMethodCall((MethodCallExpression)exp);
                case ExpressionType.Lambda:
                    return this.VisitLambda((LambdaExpression)exp);
                case ExpressionType.New:
                    return this.VisitNew((NewExpression)exp);
                case ExpressionType.NewArrayInit:
                case ExpressionType.NewArrayBounds:
                    return this.VisitNewArray((NewArrayExpression)exp);
                case ExpressionType.Invoke:
                    return this.VisitInvocation((InvocationExpression)exp);
                case ExpressionType.MemberInit:
                //return this.VisitMemberInit((MemberInitExpression)exp);
                case ExpressionType.ListInit:
                //return this.VisitListInit((ListInitExpression)exp);
                default:
                    throw new NotImplementedException(string.Format("Unhandled expression type: '{0}'", exp.NodeType));
            }
        }

        private CodeExpression VisitNew(NewExpression newExpression)
        {
            return new CodeObjectCreateExpression(
                new CodeTypeReference(newExpression.Type),
                VisitExpressionList(newExpression.Arguments).ToArray()
            );
        }

        private CodeExpression VisitInvocation(InvocationExpression invocationExpression)
        {
            CodeExpression to = _Visit(invocationExpression.Expression);

            if (typeof(CodeMethodInvokeExpression).IsAssignableFrom(to.GetType()))
            {
                CodeMethodInvokeExpression mi = to as CodeMethodInvokeExpression;
                if (invocationExpression.Arguments.Count > 0)
                    foreach (CodeExpression par in VisitExpressionList((invocationExpression.Arguments[0] as NewArrayExpression).Expressions))
                    {
                        mi.Parameters.Add(par);
                    }
                return mi;
            }
            else if (to is CodeDelegateInvokeExpression)
            {
                if (invocationExpression.Arguments.Count > 0)
                    foreach (CodeExpression par in VisitExpressionList((invocationExpression.Arguments[0] as NewArrayExpression).Expressions))
                    {
                        (to as CodeDelegateInvokeExpression).Parameters.Add(par);
                    }
                return to;
            }
            else
            {
                var mi = new CodeDelegateInvokeExpression(to);
                if (invocationExpression.Arguments.Count > 0)
                    foreach (CodeExpression par in VisitExpressionList(invocationExpression.Arguments))
                    {
                        mi.Parameters.Add(par);
                    }
                return mi;
            }

        }

        //public CodeExpressionCollection VisitExpressionList(System.Collections.ObjectModel.ReadOnlyCollection<Expression> original)
        public CodeExpressionCollection VisitExpressionList(IEnumerable<Expression> original)
        {
            CodeExpressionCollection list = new CodeExpressionCollection();
            foreach (Expression e in original)
            {
                list.Add(_Visit(e));
            }
            return list;
        }

        private CodeExpressionCollection VisitSequence(LambdaExpression lambda)
        {
            var me = lambda.Body as MethodCallExpression;
            if (me == null)
                throw new NotSupportedException();

            return VisitExpressionList((me.Arguments[0] as NewArrayExpression).Expressions);
        }

        private CodeExpression VisitNewArray(NewArrayExpression newArrayExpression)
        {
            throw new NotImplementedException();
        }

        private CodeExpression VisitUnary(UnaryExpression unaryExpression)
        {
            var c = _Visit(unaryExpression.Operand);
            switch (unaryExpression.NodeType)
            {
                case ExpressionType.Convert:
                    if (unaryExpression.Type != typeof(Object))
                    {
                        if (unaryExpression.Method == null)
                            return new CodeCastExpression(unaryExpression.Type, c);
                        else
                        {
                            Type dt = unaryExpression.Method.DeclaringType;
                            if (dt == typeof(Var) ||
                                (dt.IsGenericType &&
                                    (dt.GetGenericTypeDefinition() == typeof(VarRef<>)) ||
                                    (dt.GetGenericTypeDefinition() == typeof(ParamRef<>)) ||
                                    (dt.GetGenericTypeDefinition() == typeof(MemberRef<>))
                                )
                            )
                                return c;
                            else
                                return new CodeCastExpression(unaryExpression.Type, c);
                        }
                    }
                    if (c != null)
                        return c;
                    break;
                case ExpressionType.Quote:
                    if (c != null)
                        return c;
                    break;
            }
            throw new NotImplementedException();
        }

        public static CodeMethodReferenceExpression GetMethodRef(System.Reflection.MethodInfo methodInfo)
        {
            var c = new CodeMethodReferenceExpression()
            {
                MethodName = methodInfo.Name
            };
            if (methodInfo.IsStatic)
                c.MethodName = methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
            return c;
        }

        private CodeExpression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            var mr = GetMethodRef(methodCallExpression.Method);
            if (methodCallExpression.Object == null)
            {
                if (mr.MethodName == "LinqToCodedom.Generator.CodeDom.VarRef" ||
                    mr.MethodName == "LinqToCodedom.Generator.CodeDom.ParamRef")
                {
                    return new CodeVariableReferenceExpression(
                        CodeDom.Eval<string>(methodCallExpression.Arguments[0]));
                }
                else if (mr.MethodName == "LinqToCodedom.Generator.CodeDom.get_nil")
                {
                    return null;
                }
                else if (mr.MethodName == "LinqToCodedom.Generator.CodeDom.Property")
                {
                    return new CodePropertyReferenceExpression(
                        _Visit(methodCallExpression.Arguments[0]),
                        CodeDom.Eval<string>(methodCallExpression.Arguments[1]));
                }
                else if (mr.MethodName == "LinqToCodedom.Generator.CodeDom.Call")
                {
                    return new CodeMethodInvokeExpression(
                        _Visit(methodCallExpression.Arguments[0]),
                        CodeDom.Eval<string>(methodCallExpression.Arguments[1]));
                }
                else if (mr.MethodName == "LinqToCodedom.Generator.CodeDom.new")
                {
                    object t = CodeDom.Eval(methodCallExpression.Arguments[0]);
                    CodeTypeReference type = CodeDom.GetTypeReference(t);

                    if (methodCallExpression.Arguments.Count == 2)
                    {
                        NewArrayExpression arr = methodCallExpression.Arguments[1] as NewArrayExpression;
                        return new CodeObjectCreateExpression(type, VisitExpressionList(arr.Expressions).ToArray());
                    }
                    else
                        return new CodeObjectCreateExpression(type);
                }
                //else if (mr.MethodName == "LinqToCodedom.Generator.Builder.newOf")
                //{
                //    object t = Builder.Eval(methodCallExpression.Arguments[0]);
                //    CodeTypeReference type = null;
                //    if (t is string)
                //        type = new CodeTypeReference(t as string);
                //    else if (t is Type)
                //        type = new CodeTypeReference(t as Type);
                //    else
                //        throw new NotSupportedException();

                //    NewArrayExpression types = methodCallExpression.Arguments[1] as NewArrayExpression;
                //    foreach (Expression e in types.Expressions)
                //    {
                //        CodeTypeReference gt = null;
                //        object v = Builder.Eval(e);
                //        if (v is string)
                //            gt = new CodeTypeReference(v as string);
                //        else if (v is Type)
                //            gt = new CodeTypeReference(v as Type);
                //        else
                //            throw new NotSupportedException();
                //        type.TypeArguments.Add(gt);
                //    }

                //    if (methodCallExpression.Arguments.Count == 3)
                //    {
                //        NewArrayExpression arr = methodCallExpression.Arguments[2] as NewArrayExpression;
                //        return new CodeObjectCreateExpression(type, VisitExpressionList(arr.Expressions).ToArray());
                //    }
                //    else
                //        return new CodeObjectCreateExpression(type);
                //}
            }

            var to = _Visit(methodCallExpression.Object);
            if (to is CodeDom.CodeThisExpression || to is CodeDom.CodeBaseExpression || to is CodeDom.CodeVarExpression)
            {
                CodeExpression rto = to is CodeDom.CodeThisExpression ?
                    new CodeThisReferenceExpression() :
                    to is CodeDom.CodeBaseExpression ?
                        new CodeBaseReferenceExpression() as CodeExpression :
                        to as CodeVariableReferenceExpression;

                switch (mr.MethodName)
                {
                    case "Call":
                        if (methodCallExpression.Arguments.Count > 0)
                        {
                            string methodName = CodeDom.Eval<string>(methodCallExpression.Arguments[0]);
                            if (methodCallExpression.Arguments.Count > 1)
                                throw new NotImplementedException();
                            return new CodeDom.CodeArgsInvoke(rto, methodName);
                        }
                        else
                        {
                            return new CodeDelegateInvokeExpression(rto);
                        }
                    case "Property":
                        string propertyName = CodeDom.Eval<string>(methodCallExpression.Arguments[0]);
                        if (methodCallExpression.Arguments.Count > 1)
                            throw new NotImplementedException();
                        return new CodePropertyReferenceExpression(rto, propertyName);
                    case "Field":
                        string fieldName = CodeDom.Eval<string>(methodCallExpression.Arguments[0]);
                        if (methodCallExpression.Arguments.Count > 1)
                            throw new NotImplementedException();
                        return new CodeFieldReferenceExpression(rto, fieldName);
                    default:
                        throw new NotImplementedException(mr.MethodName);
                }
            }
            else if (to is CodeDom.CodeArgsInvoke)
            {
                var c = to as CodeMethodInvokeExpression;
                foreach (CodeExpression par in VisitSequence(
                    new QueryVisitor((e) => e is LambdaExpression)
                        .Visit(methodCallExpression.Arguments[0]) as LambdaExpression))
                {
                    c.Parameters.Add(par);
                }
                return c;
            }
            else
            {
                var c = new CodeMethodInvokeExpression(mr);
                foreach (var par in methodCallExpression.Arguments)
                {
                    c.Parameters.Add(_Visit(par));
                }
                c.Method.TargetObject = to;
                return c;
            }
        }

        private CodeExpression VisitBinary(BinaryExpression binaryExpression)
        {
            CodeBinaryOperatorType operType;

            switch (binaryExpression.NodeType)
            {
                case ExpressionType.Add:
                    operType = CodeBinaryOperatorType.Add;
                    break;
                case ExpressionType.AddChecked:
                    operType = CodeBinaryOperatorType.Add;
                    break;
                case ExpressionType.And:
                    operType = CodeBinaryOperatorType.BooleanAnd;
                    break;
                case ExpressionType.AndAlso:
                    operType = CodeBinaryOperatorType.BooleanAnd;
                    break;
                case ExpressionType.Divide:
                    operType = CodeBinaryOperatorType.Divide;
                    break;
                case ExpressionType.Equal:
                    operType = CodeBinaryOperatorType.ValueEquality;
                    break;
                case ExpressionType.ExclusiveOr:
                    operType = CodeBinaryOperatorType.BooleanOr;
                    break;
                case ExpressionType.GreaterThan:
                    operType = CodeBinaryOperatorType.GreaterThan;
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    operType = CodeBinaryOperatorType.GreaterThanOrEqual;
                    break;
                case ExpressionType.Invoke:

                case ExpressionType.Lambda:

                case ExpressionType.LeftShift:

                case ExpressionType.LessThan:
                    operType = CodeBinaryOperatorType.LessThan;
                    break;
                case ExpressionType.LessThanOrEqual:
                    operType = CodeBinaryOperatorType.LessThanOrEqual;
                    break;
                case ExpressionType.Modulo:
                    operType = CodeBinaryOperatorType.Modulus;
                    break;
                case ExpressionType.Multiply:
                    operType = CodeBinaryOperatorType.Multiply;
                    break;
                case ExpressionType.MultiplyChecked:
                    operType = CodeBinaryOperatorType.Multiply;
                    break;
                case ExpressionType.Negate:

                case ExpressionType.NegateChecked:

                case ExpressionType.Not:

                case ExpressionType.NotEqual:
                    operType = CodeBinaryOperatorType.IdentityInequality;
                    break;
                case ExpressionType.Or:
                    operType = CodeBinaryOperatorType.BooleanOr;
                    break;
                case ExpressionType.OrElse:
                    operType = CodeBinaryOperatorType.BooleanOr;
                    break;
                case ExpressionType.Power:

                case ExpressionType.RightShift:

                case ExpressionType.Subtract:
                    operType = CodeBinaryOperatorType.Subtract;
                    break;
                case ExpressionType.SubtractChecked:
                    operType = CodeBinaryOperatorType.Subtract;
                    break;
                case ExpressionType.UnaryPlus:
                default:
                    throw new NotImplementedException();
            }

            return new CodeBinaryOperatorExpression(
                        _Visit(binaryExpression.Left), operType, _Visit(binaryExpression.Right));
        }

        private CodeExpression VisitLambda(LambdaExpression lambdaExpression)
        {
            _ctx.VisitParams(lambdaExpression.Parameters);

            var c = _Visit(lambdaExpression.Body);

            if (c.GetType() == typeof(CodeDom.CodeNilExpression))
                return _ctx.Params[0];

            return c;
        }

        private CodeExpression VisitConstant(ConstantExpression constantExpression)
        {
            if (constantExpression.Value == null)
                return new CodePrimitiveExpression(null);
            else
            {
                Type t = constantExpression.Value.GetType();
                if (t.IsEnum)
                    return new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(t),
                        constantExpression.Value.ToString());
                else if (typeof(Type).IsAssignableFrom(t))
                    return new CodeTypeOfExpression(constantExpression.Value as Type);
                else if (typeof(System.Reflection.MemberInfo).IsAssignableFrom(t))
                {
                    System.Reflection.MemberInfo mi = constantExpression.Value as System.Reflection.MemberInfo;
                    return new CodePrimitiveExpression(mi.Name);
                }
                else
                    return new CodePrimitiveExpression(constantExpression.Value);
            }
        }

        private CodeExpression VisitMemberAccess(MemberExpression memberExpression)
        {
            if (memberExpression.Expression == null)
            {
                if (memberExpression.Type == typeof(CodeDom.NilClass))
                {
                    if (memberExpression.Member.Name == "nil")
                    {
                        return new CodeDom.CodeNilExpression();
                    }
                    else
                        throw new NotImplementedException();
                }
                else if (memberExpression.Type == typeof(This))
                {
                    if (memberExpression.Member.Name == "this")
                        return new CodeDom.CodeThisExpression();
                    else if (memberExpression.Member.Name == "base")
                        return new CodeDom.CodeBaseExpression();
                    else
                        throw new NotImplementedException();
                }
                else
                    return null;
            }
            else
            {
                var c = _Visit(memberExpression.Expression);
                if (c is CodeSnippetExpression)
                    throw new NotImplementedException();
                else
                    return c;
            }
        }
    }
}
