using System.Globalization;

namespace ElectricalComponentSandbox.Models;

internal static class ProjectParameterFormulaEvaluator
{
    public static void EvaluateAll(IReadOnlyList<ProjectParameterDefinition> parameters, bool throwOnError)
    {
        var session = new EvaluationSession(parameters, throwOnError);
        session.EvaluateAll();
    }

    private sealed class EvaluationSession
    {
        private readonly IReadOnlyList<ProjectParameterDefinition> _parameters;
        private readonly bool _throwOnError;
        private readonly Dictionary<string, ProjectParameterDefinition> _parametersByName;
        private readonly Dictionary<string, double> _resolvedValues = new(StringComparer.Ordinal);
        private readonly HashSet<string> _evaluationStack = new(StringComparer.Ordinal);

        public EvaluationSession(IReadOnlyList<ProjectParameterDefinition> parameters, bool throwOnError)
        {
            _parameters = parameters;
            _throwOnError = throwOnError;
            _parametersByName = parameters.ToDictionary(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase);
        }

        public void EvaluateAll()
        {
            foreach (var parameter in _parameters)
                parameter.FormulaError = null;

            foreach (var parameter in _parameters)
                EvaluateParameter(parameter);
        }

        private double EvaluateParameter(ProjectParameterDefinition parameter)
        {
            if (_resolvedValues.TryGetValue(parameter.Id, out var cachedValue))
                return cachedValue;

            if (!_evaluationStack.Add(parameter.Id))
                return HandleFailure(parameter, $"Circular reference detected involving '{parameter.Name}'.");

            try
            {
                if (!parameter.SupportsFormula && !string.IsNullOrWhiteSpace(parameter.Formula))
                    return HandleFailure(parameter, "Text parameters do not support formulas.");

                var value = parameter.HasFormula
                    ? ProjectParameterFormulaParser.Parse(parameter.Formula, ResolveReference)
                    : parameter.Value;

                if (double.IsNaN(value) || double.IsInfinity(value))
                    return HandleFailure(parameter, "Formula result must be a finite number.");

                parameter.FormulaError = null;
                parameter.Value = value;
                _resolvedValues[parameter.Id] = value;
                return value;
            }
            catch (Exception ex)
            {
                return HandleFailure(parameter, ex.Message, ex);
            }
            finally
            {
                _evaluationStack.Remove(parameter.Id);
            }
        }

        private double ResolveReference(string parameterName)
        {
            if (!_parametersByName.TryGetValue(parameterName, out var referencedParameter))
                throw new InvalidOperationException($"Unknown parameter '{parameterName}'.");

            return EvaluateParameter(referencedParameter);
        }

        private double HandleFailure(ProjectParameterDefinition parameter, string message, Exception? innerException = null)
        {
            parameter.FormulaError = message;
            if (_throwOnError)
                throw new InvalidOperationException($"Formula for '{parameter.Name}' is invalid: {message}", innerException);

            _resolvedValues[parameter.Id] = parameter.Value;
            return parameter.Value;
        }
    }

    private sealed class ProjectParameterFormulaParser
    {
        private readonly string _formula;
        private readonly Func<string, double> _resolveReference;
        private int _position;

        private ProjectParameterFormulaParser(string formula, Func<string, double> resolveReference)
        {
            _formula = formula;
            _resolveReference = resolveReference;
        }

        public static double Parse(string formula, Func<string, double> resolveReference)
        {
            var parser = new ProjectParameterFormulaParser(formula, resolveReference);
            var value = parser.ParseExpression();
            parser.SkipWhitespace();
            if (!parser.IsAtEnd)
                throw new InvalidOperationException($"Unexpected token at position {parser._position + 1}.");

            return value;
        }

        private bool IsAtEnd => _position >= _formula.Length;

        private double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('+'))
                {
                    value += ParseTerm();
                }
                else if (TryConsume('-'))
                {
                    value -= ParseTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseTerm()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('*'))
                {
                    value *= ParseFactor();
                }
                else if (TryConsume('/'))
                {
                    var denominator = ParseFactor();
                    if (Math.Abs(denominator) <= 1e-12)
                        throw new InvalidOperationException("Division by zero is not allowed.");

                    value /= denominator;
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseFactor()
        {
            SkipWhitespace();

            if (TryConsume('+'))
                return ParseFactor();

            if (TryConsume('-'))
                return -ParseFactor();

            if (TryConsume('('))
            {
                var nested = ParseExpression();
                SkipWhitespace();
                if (!TryConsume(')'))
                    throw new InvalidOperationException("Missing closing parenthesis.");

                return nested;
            }

            if (TryConsume('['))
                return ParseReference();

            return ParseNumber();
        }

        private double ParseReference()
        {
            var start = _position;
            while (!IsAtEnd && _formula[_position] != ']')
                _position++;

            if (IsAtEnd)
                throw new InvalidOperationException("Missing closing bracket for parameter reference.");

            var parameterName = _formula[start.._position].Trim();
            _position++;

            if (string.IsNullOrWhiteSpace(parameterName))
                throw new InvalidOperationException("Parameter references cannot be empty.");

            return _resolveReference(parameterName);
        }

        private double ParseNumber()
        {
            SkipWhitespace();
            var start = _position;

            while (!IsAtEnd && (char.IsDigit(_formula[_position]) || _formula[_position] == '.'))
                _position++;

            if (start == _position)
                throw new InvalidOperationException($"Expected a number or [Parameter Name] at position {start + 1}.");

            var token = _formula[start.._position];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException($"'{token}' is not a valid decimal number.");

            return value;
        }

        private bool TryConsume(char expected)
        {
            SkipWhitespace();
            if (!IsAtEnd && _formula[_position] == expected)
            {
                _position++;
                return true;
            }

            return false;
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(_formula[_position]))
                _position++;
        }
    }
}