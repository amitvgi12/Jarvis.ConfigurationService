using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Jarvis.ConfigurationService.Host.Support
{
    internal class ParameterManager
    {
        internal class ReplaceResult
        {
            public Boolean HasReplaced { get; set; }

            public HashSet<String> MissingParams { get; set; }

            public ReplaceResult()
            {
                MissingParams = new HashSet<string>();
            }

            internal void Merge(ReplaceResult result)
            {
                HasReplaced = HasReplaced || result.HasReplaced;
                foreach (var missingParam in result.MissingParams)
                {
                    MissingParams.Add(missingParam);
                }
            }
        }

        private class ParameterValue
        {
            public static ParameterValue Create(String value)
            {
                return new ParameterValue(false, value);
            }

            /// <summary>
            /// The caller could ask to use a specific string for missing parameter, but
            /// we need to mark that the parameter was really missing.
            /// </summary>
            /// <param name="value"></param>
            /// <returns></returns>
            public static ParameterValue CreateForMissingValue(String value)
            {
                return new ParameterValue(true, value);
            }

            private ParameterValue(bool isMissing, string value)
            {
                IsMissing = isMissing;
                Value = value;
            }

            public Boolean IsMissing { get; set; }

            public String Value { get; set; }
        }

        private readonly String _missingParametersToken;

        /// <summary>
        /// Create the parameter manager object.
        /// </summary>
        /// <param name="missingParametersToken">If a parameter is missing we can
        /// substituite this token to the missing parameter instead of marking the 
        /// parameter as missing.</param>
        public ParameterManager(String missingParametersToken = null)
        {
            _missingParametersToken = missingParametersToken;
        }

        /// <summary>
        /// it is used to retrieve parameters settings from config file.
        /// </summary>
        /// <param name="settingName"></param>
        /// <param name="parameterObject"></param>
        /// <returns></returns>
        private ParameterValue GetParameterValue(string settingName, JObject parameterObject)
        {
            var path = settingName.Split('.');
            JObject current = parameterObject;
            for (int i = 0; i < path.Length - 1; i++)
            {
                if (current[path[i]] == null) return ParameterValue.CreateForMissingValue( _missingParametersToken);
                current = (JObject)current[path[i]];
            }
            if (current[path.Last()] == null)
                return ParameterValue.CreateForMissingValue(_missingParametersToken);
            return ParameterValue.Create(current[path.Last()].ToString());
        }

        internal ReplaceResult ReplaceParameters(JObject source, JObject parameterObject)
        {
            ReplaceResult result = new ReplaceResult();
            foreach (var property in source.Properties())
            {
                if (property.Value is JObject)
                {
                    var replaceReturn = ReplaceParameters((JObject)property.Value, parameterObject);
                    result.Merge(replaceReturn);
                }
                else if (property.Value is JArray)
                {
                    ReplaceParametersInArray(parameterObject, result, property.Value as JArray);
                }
                else if (property.Value is JToken)
                {
                    source[property.Name] = ManageParametersInJToken(parameterObject, result, property.Value);
                }
            }
            return result;
        }

        internal String ReplaceParametersInString(String source, JObject parameterObject)
        {
            ReplaceResult result = new ReplaceResult();
            var newValue = Regex.Replace(
                    source,
                    @"(?<!%)%(?!%)(?<match>.+?)(?<!%)%(?!%)",
                    new MatchEvaluator(m =>
                    {
                        var parameterName = m.Groups["match"].Value.Trim('{', '}');
                        var paramValue = GetParameterValue(parameterName, parameterObject);
                        if (paramValue.IsMissing)
                        {
                            result.MissingParams.Add(parameterName);
                            return paramValue.Value ?? "%" + parameterName + "%";
                        }
                        result.HasReplaced = true;
                        return paramValue.Value;
                    }));
            if (result.MissingParams.Count > 0)
            {
                throw new ConfigurationErrorsException("Missing parameters: " +
                   result.MissingParams.Aggregate((s1, s2) => s1 + ", " + s2));
            }
            if (newValue.Contains("%%"))
            {
                newValue = newValue.Replace("%%", "%");
            }
            return newValue;
        }

        private void ReplaceParametersInArray(
            JObject parameterObject,
            ReplaceResult result,
            JArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var element = array[i];

                if (element is JObject)
                {
                    var replaceReturn = ReplaceParameters((JObject)element, parameterObject);
                    result.Merge(replaceReturn);
                }
                else if (element is JArray)
                {
                    ReplaceParametersInArray(parameterObject, result, element as JArray);
                }
                else if (element is JToken)
                {
                    array[i] = ManageParametersInJToken(parameterObject, result, element);
                }
            }
        }

        private JToken ManageParametersInJToken(
            JObject parameterObject,
            ReplaceResult result,
            JToken token)
        {
            String value = token.ToString();
            Boolean objectParameter = value.StartsWith("%{") && value.EndsWith("}%");
            if (Regex.IsMatch(value, "(?<!%)%(?!%).+?(?<!%)%(?!%)"))
            {
                String newValue = Regex.Replace(
                    value,
                    @"(?<!%)%(?!%)(?<match>.+?)(?<!%)%(?!%)",
                    new MatchEvaluator(m =>
                    {
                        var parameterName = m.Groups["match"].Value.Trim('{', '}');
                        var paramValue = GetParameterValue(parameterName, parameterObject);
                        if (paramValue.IsMissing)
                        {
                            result.MissingParams.Add(parameterName);
                            return paramValue.Value ?? (objectParameter ? value : "%" + parameterName + "%");
                        }
                        result.HasReplaced = true;
                        return paramValue.Value;
                    }));
                if (objectParameter)
                {
                    if (newValue == value) return newValue; //object parameter that is missing
                    try
                    {
                        return (JToken)JsonConvert.DeserializeObject(newValue);
                    }
                    catch (Exception)
                    {
                        //parameters has some error, maybe it is malformed, treat as if parameter is missing.
                        return "Parameter " + value + " is an object parameter and cannot be parsed: " + newValue;
                    }
                }
                else
                {
                    return newValue;
                }
            }
            return token;
        }

        internal void UnescapePercentage(JObject source)
        {
            foreach (var property in source.Properties())
            {
                if (property.Value is JObject)
                {
                    UnescapePercentage((JObject)property.Value);
                }
                else if (property.Value is JToken && property.Value.ToString().Contains("%%"))
                {
                    source[property.Name] = property.Value.ToString().Replace("%%", "%");
                }
            }
        }
    }
}