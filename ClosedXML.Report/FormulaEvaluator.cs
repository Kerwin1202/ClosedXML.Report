﻿using ClosedXML.Report.Utils;
using DocumentFormat.OpenXml.Bibliography;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace ClosedXML.Report
{
    internal class FormulaEvaluator
    {
        private static readonly Regex ExprMatch = new Regex(@"\{\{.+?\}\}");
        //  private readonly Interpreter _interpreter; !!! переделать на DynamicLinq
        private readonly Dictionary<string, Delegate> _lambdaCache = new Dictionary<string, Delegate>();
        private readonly Dictionary<string, object> _variables = new Dictionary<string, object>();

        public object Evaluate(string formula, params Parameter[] pars)
        {
            var expressions = GetExpressions(formula);
            foreach (var expr in expressions)
            {
                var val = Eval(expr.Substring(2, expr.Length - 4), pars);
                if (expr == formula)
                    return val;

                formula = formula.Replace(expr, ObjToString(val));
            }
            return formula;
        }

        public bool TryEvaluate(string formula, out object result, params Parameter[] pars)
        {
            try
            {
                result = Evaluate(formula, pars);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        public void AddVariable(string name, object value)
        {
            _variables[name]=value;
        }

        private string ObjToString(object val)
        {
            if (val == null) val = "";
            if (val is DateTime dateVal)
                return dateVal.ToOADate().ToString(CultureInfo.InvariantCulture);

            return val is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : val.ToString();
        }

        private IEnumerable<string> GetExpressions(string cellValue)
        {
            var matches = ExprMatch.Matches(cellValue);
            return from Match match in matches select match.Value;
        }

        private object Eval(string expression, Parameter[] pars)
        {
            if (!_lambdaCache.TryGetValue(expression, out var lambda))
            {
                var parameters = pars.Select(p=>p.ParameterExpression).ToArray();
                try
                {
                    lambda = XLDynamicExpressionParser.ParseLambda(parameters, typeof(object), expression, _variables).Compile();
                }
                catch (ArgumentException)
                {
                    return null;
                }

                _lambdaCache.Add(expression, lambda);
            }

            return lambda.DynamicInvoke(pars.Select(p => p.Value).ToArray());
        }
    }

    internal class Parameter
    {
        public Parameter(string name, object value)
        {
            ParameterExpression = Expression.Parameter(value?.GetType() ?? typeof(string), name);
            Value = value;
        }

        public ParameterExpression ParameterExpression { get; }
        public object Value { get; }
    }
}
