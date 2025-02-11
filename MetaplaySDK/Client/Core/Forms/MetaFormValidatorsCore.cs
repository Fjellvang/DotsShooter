// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Localization;
using System;
using System.Globalization;

namespace Metaplay.Core.Forms
{
    /// <summary>
    /// On a Dashboard form, the value for this field must be given.
    /// </summary>
    public class MetaValidateRequiredAttribute : MetaFormFieldValidatorBaseAttribute
    {
        public class Validator : IMetaFormValidator
        {
            readonly MetaValidateRequiredAttribute _attribute;

            public Validator(MetaValidateRequiredAttribute attribute)
            {
                _attribute = attribute;
            }

            public void Validate(object fieldOrForm, FormValidationContext ctx)
            {
                if (fieldOrForm == null)
                {
                    ctx.Fail(_attribute.ErrorMessage);
                    return;
                }

                if (fieldOrForm is string fieldString)
                {
                    if (string.IsNullOrEmpty(fieldString))
                        ctx.Fail(_attribute.ErrorMessage);
                }
                else if (fieldOrForm is LocalizedString localizedString)
                {
                    if (localizedString.Localizations == null)
                        ctx.Fail(_attribute.ErrorMessage);
                    else
                    {
                        foreach (MetaDictionary<LanguageId, string>.KeyValue keyValue in localizedString.Localizations)
                        {
                            if (string.IsNullOrEmpty(keyValue.Value))
                                ctx.Fail(_attribute.ErrorMessage, $"{nameof(LocalizedString.Localizations)}/{keyValue.Key}");
                        }
                    }
                }
                else if (fieldOrForm is MetaTime metaTime)
                {
                    if(metaTime == default)
                        ctx.Fail(_attribute.ErrorMessage);
                }
            }
        }

        string ErrorMessage { get; }

        public MetaValidateRequiredAttribute(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public MetaValidateRequiredAttribute()
        {
            ErrorMessage = "This field is required.";
        }

        public override string ValidationRuleName => "notEmpty";

        public override object ValidationRuleProps => new
        {
            message = ErrorMessage,
        };

        public override Type CustomValidatorType => typeof(Validator);
    }

    /// <summary>
    /// On a Dashboard form, the value for this field must be given such that it was in the future when it is given.
    /// </summary>
    public class MetaValidateInFutureAttribute : MetaFormFieldValidatorBaseAttribute
    {
        public class Validator : IMetaFormValidator
        {
            readonly MetaValidateInFutureAttribute _attribute;

            public Validator(MetaValidateInFutureAttribute attribute)
            {
                _attribute = attribute;
            }

            public void Validate(object fieldOrForm, FormValidationContext ctx)
            {
                if (fieldOrForm == null)
                {
                    ctx.Fail("value is null");
                    return;
                }

                if (fieldOrForm is MetaTime metaTime)
                {
                    if (metaTime < MetaTime.Now)
                        ctx.Fail(_attribute.ErrorMessage);
                }
            }
        }

        string ErrorMessage { get; }

        public MetaValidateInFutureAttribute(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public MetaValidateInFutureAttribute()
        {
            ErrorMessage = "Field must be a time from the future!";
        }

        public override string ValidationRuleName  => "inFuture";
        public override object ValidationRuleProps => new {message = ErrorMessage};
        public override Type   CustomValidatorType => typeof(Validator);
    }

    /// <summary>
    /// On a Dashboard form, the value for this field must be within the defined range.
    /// Supports min and max values within the range of Int64/long. Validation fails
    /// for values outside that range.
    /// </summary>
    public class MetaValidateInRangeAttribute : MetaFormFieldValidatorBaseAttribute
    {
        public class Validator : IMetaFormValidator
        {
            readonly MetaValidateInRangeAttribute _attribute;

            public Validator(MetaValidateInRangeAttribute attribute)
            {
                _attribute = attribute;
            }

            public void Validate(object fieldOrForm, FormValidationContext ctx)
            {
                try
                {
                    if (fieldOrForm == null)
                    {
                        ctx.Fail("value is null");
                        return;
                    }

                    long valueAsInt64 = Convert.ToInt64(fieldOrForm, CultureInfo.InvariantCulture);
                    if (valueAsInt64 < _attribute.Min || valueAsInt64 > _attribute.Max)
                    {
                        ctx.Fail(_attribute.ErrorMessage);
                    }
                }
                catch (OverflowException)
                {
                    ctx.Fail($"Value is not in supported range of {typeof(long)}.");
                }
            }
        }

        string      ErrorMessage { get; }
        public long Min          { get; }
        public long Max          { get; }

        public MetaValidateInRangeAttribute(int min, int max)
            : this(min, max, FormattableString.Invariant($"value must be in range of {min} and {max}")) { }

        public MetaValidateInRangeAttribute(int min, int max, string message)
        {
            Min          = min;
            Max          = max;
            ErrorMessage = message;
        }

        public MetaValidateInRangeAttribute(long min, long max)
            : this(min, max, FormattableString.Invariant($"value must be in range of {min} and {max}")) { }

        public MetaValidateInRangeAttribute(long min, long max, string message)
        {
            Min          = min;
            Max          = max;
            ErrorMessage = message;
        }

        public override string ValidationRuleName => "inRange";

        public override object ValidationRuleProps => new
        {
            min     = Min,
            max     = Max,
            message = ErrorMessage,
        };

        public override Type CustomValidatorType => typeof(Validator);
    }

    /// <summary>
    /// On a Dashboard form, the floating point value for this field must be within the defined range.
    /// Supports min and max values within the range of double. Validation fails
    /// for values outside that range.
    /// </summary>
    public class MetaValidateInRangeFloatAttribute : MetaFormFieldValidatorBaseAttribute
    {
        public class Validator : IMetaFormValidator
        {
            readonly MetaValidateInRangeFloatAttribute _attribute;

            public Validator(MetaValidateInRangeFloatAttribute attribute)
            {
                _attribute = attribute;
            }

            public void Validate(object fieldOrForm, FormValidationContext ctx)
            {
                try
                {
                    if (fieldOrForm == null)
                    {
                        ctx.Fail("value is null");
                        return;
                    }

                    double valueAsDouble = Convert.ToDouble(fieldOrForm, CultureInfo.InvariantCulture);
                    if (valueAsDouble < _attribute.Min || valueAsDouble > _attribute.Max)
                    {
                        ctx.Fail(_attribute.ErrorMessage);
                    }
                }
                catch (OverflowException)
                {
                    ctx.Fail($"Value is not in supported range of {typeof(double)}.");
                }
            }
        }

        string        ErrorMessage { get; }
        public double Min          { get; }
        public double Max          { get; }

        public MetaValidateInRangeFloatAttribute(float min, float max)
            : this (min, max, FormattableString.Invariant($"value must be in range of {min} and {max}")) { }

        public MetaValidateInRangeFloatAttribute(float min, float max, string message)
        {
            Min          = min;
            Max          = max;
            ErrorMessage = message;
        }

        public MetaValidateInRangeFloatAttribute(double min, double max)
            : this(min, max, FormattableString.Invariant($"value must be in range of {min} and {max}")) { }

        public MetaValidateInRangeFloatAttribute(double min, double max, string message)
        {
            Min          = min;
            Max          = max;
            ErrorMessage = message;
        }

        public override string ValidationRuleName => "inRange";

        public override object ValidationRuleProps => new
        {
            min     = Min,
            max     = Max,
            message = ErrorMessage,
        };

        public override Type CustomValidatorType => typeof(Validator);
    }
}
