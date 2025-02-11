// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud;
using Metaplay.Core;
using Metaplay.Core.Forms;
using Metaplay.Core.Localization;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using Metaplay.Core.Schedule;
using Metaplay.Core.Serialization;
using Metaplay.Server.Database;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for various endpoints devoted to testing
    /// </summary>
    public class TestingController : GameAdminApiController
    {
        IMetaLogger _log = MetaLogger.ForContext<TestingController>();

        /// <summary>
        /// HTTP request/response formats
        /// </summary>
        public class NewPlayerResponse
        {
            public EntityId Id;
            public NewPlayerResponse(EntityId id) { Id = id; }
        }

        /// <summary>
        /// Creates a new player without any authentication
        /// Usage:  POST /api/testing/createPlayer
        /// Test:   curl http://localhost:5550/api/testing/createPlayer --request POST
        /// </summary>
        [HttpPost("testing/createPlayer")]
        [RequirePermission(MetaplayPermissions.ApiTestsAll)]
        public async Task<ActionResult<GlobalStatusResponse>> CreatePlayer()
        {
            EntityId playerId = await DatabaseEntityUtil.CreateNewPlayerAsync(_log);
            await TellEntityAsync(playerId, PlayerResetState.Instance);
            return Ok(new NewPlayerResponse(playerId));
        }

        public class AllFormTypesResponse
        {
            public List<string> AllTypes;
            public List<string> ExampleTypes;

            public AllFormTypesResponse(List<string> allTypes, List<string> exampleTypes)
            {
                AllTypes = allTypes;
                ExampleTypes = exampleTypes;
            }
        }

        /// <summary>
        /// Gets all types that could technically show up in a generated form or view and returns their full names in a list,
        /// and additionally returns a list of example types one might see in generated forms.
        /// Usage:  GET /api/testing/getAllFormTypes
        /// Test:   curl http://localhost:5550/api/testing/getAllFormTypes
        /// </summary>
        [HttpGet("testing/getAllFormTypes")]
        [RequirePermission(MetaplayPermissions.ApiTestsAll)]
        public ActionResult<AllFormTypesResponse> GetAllFormTypes()
        {
            List<string> allTypesNames = MetaplayServices.Get<MetaSerializerTypeRegistry>().AllTypes.Select(type => type.Type.FullName).ToList();

            List<string> exampleTypesNames = new List<string>()
            {
                typeof(FormTestingPrimitives).FullName,
                typeof(FormTestingCollections).FullName,
                typeof(FormTestingDictionaries).FullName,
                typeof(FormTestingClasses).FullName,
                typeof(FormTestingLocalized).FullName,
                typeof(FormTestingMetaTime).FullName,
                typeof(FormTestingAbstractClass).FullName,
                typeof(FormTestingAttributes).FullName,
            };

            return new AllFormTypesResponse(allTypesNames, exampleTypesNames);
        }

        [MetaSerializable]
        public class FormTestingPrimitives
        {
            [MetaMember(1)]  public bool                                   BoolTest                    { get; set; }
            [MetaMember(2)]  public sbyte                                  SByteTest                   { get; set; }
            [MetaMember(3)]  public byte                                   ByteTest                    { get; set; }
            [MetaMember(4)]  public short                                  ShortTest                   { get; set; }
            [MetaMember(5)]  public ushort                                 UShortTest                  { get; set; }
            [MetaMember(6)]  public char                                   CharTest                    { get; set; }
            [MetaMember(7)]  public int                                    IntTest                     { get; set; }
            [MetaMember(8)]  public uint                                   UIntTest                    { get; set; }
            [MetaMember(9)]  public long                                   LongTest                    { get; set; }
            [MetaMember(10)] public ulong                                  ULongTest                   { get; set; }
            [MetaMember(11)] public MetaUInt128                            MetaUInt128Test             { get; set; }
            [MetaMember(12)] public F32                                    F32Test                     { get; set; }
            [MetaMember(13)] public F32Vec2                                F32Vec2Test                 { get; set; }
            [MetaMember(14)] public F32Vec3                                F32Vec3Test                 { get; set; }
            [MetaMember(15)] public F64                                    F64Test                     { get; set; }
            [MetaMember(16)] public F64Vec2                                F64Vec2Test                 { get; set; }
            [MetaMember(17)] public F64Vec3                                F64Vec3Test                 { get; set; }
            [MetaMember(18)] public float                                  FloatTest                   { get; set; }
            [MetaMember(19)] public double                                 DoubleTest                  { get; set; }
            [MetaMember(20)] public MetaGuid                               MetaGuidTest                { get; set; }
            [MetaMember(21)] public string                                 StringTest                  { get; set; }
            [MetaMember(22)] public byte[]                                 ByteArrayTest               { get; set; }
            [MetaMember(23)] public EntityKind                             EntityKindTest              { get; set; }
            [MetaMember(24)] public int?                                   NullableIntTest             { get; set; }
        }

        [MetaSerializable]
        public class FormTestingCollections
        {
            [MetaMember(1)] public int[]                                       IntArrayTest                { get; set; }
            [MetaMember(2)] public string[]                                    StringArrayTest             { get; set; }
            [MetaMember(3)] public FormTestingClass[]                          ClassArrayTest              { get; set; }
            [MetaMember(4)] public List<FormTestingClass>                      ClassListTest               { get; set; }
            [MetaMember(5)] public List<int>                                   IntListTest                 { get; set; }
            [MetaMember(6)] public int?[]                                      NullableIntArrayTest        { get; set; }
        }

        [MetaSerializable]
        public class FormTestingDictionaries
        {
            [MetaMember(1)] public MetaDictionary<int, string>                            IntStringDictionaryTest     { get; set; }
            [MetaMember(2)] public MetaDictionary<string, int>                            StringIntDictionaryTest     { get; set; }
            [MetaMember(3)] public MetaDictionary<string, FormTestingClass>               StringClassDictionaryTest   { get; set; }
            [MetaMember(4)] public MetaDictionary<string, FormTestingClassWithCollection> StringListDictionaryTest    { get; set; }
        }

        [MetaSerializable]
        public class FormTestingClasses
        {
            [MetaMember(1)] public FormTestingStruct  StructTest         { get; set; }
            [MetaMember(2)] public FormTestingStruct? NullableStructTest { get; set; }
            [MetaMember(3)] public FormTestingClass   ClassTest          { get; set; }
        }

        [MetaSerializable]
        public class FormTestingLocalized
        {
            [MetaMember(1)] public LocalizedString LocalizedStringTest { get; set; }
            [MetaMember(2)] public string NonLocalizedStringTest { get; set; }
            [MetaMember(3)] public List<LocalizedString> LocalizedStringListTest { get; set; }
        }

        [MetaSerializable]
        public class FormTestingMetaTime
        {
            [MetaMember(1)] public MetaTime MetaTimeTest { get; set; }
            [MetaMember(2)] public MetaTime? NullableMetaTimeTest { get; set; }
            [MetaMember(3)] public MetaDuration MetaDurationTest { get; set; }
            [MetaMember(4)] public MetaScheduleBase MetaScheduleBaseTest { get; set; }
        }

        [MetaSerializable]
        public class FormTestingClass
        {
            [MetaMember(1)] public int    IntTest { get; set; }
            [MetaMember(2)] public string StringTest  { get; set; }
        }

        [MetaSerializable]
        public class FormTestingClassWithCollection
        {
            [MetaMember(1)] public List<string> StringListTest { get; set; }
        }

        [MetaSerializable]
        public struct FormTestingStruct
        {
            [MetaMember(1)] public int    IntTest    { get; set; }
            [MetaMember(2)] public string StringTest { get; set; }
        }

        [MetaSerializable]
        public abstract class FormTestingAbstractClass
        {
            [MetaMember(1)] public string BaseClassField { get; set; }
        }

        [MetaSerializableDerived(1)]
        public class FormTestingFirstDerivedClass : FormTestingAbstractClass
        {
            [MetaMember(2)] public string DerivedClassStringField { get; set; }
        }

        [MetaSerializableDerived(2)]
        public class FormTestingSecondDerivedClass : FormTestingAbstractClass
        {
            [MetaMember(2)] public int DerivedClassIntField { get; set; }
        }

        [MetaFormHidden]
        [MetaSerializableDerived(3)]
        public class FormTestingHiddenDerivedClass : FormTestingAbstractClass
        {
            [MetaMember(2)] public int DerivedClassIntField { get; set; }
        }

        [MetaFormDerivedMembersOnly]
        [MetaSerializableDerived(4)]
        public class FormTestingDerivedMembersOnlyClass : FormTestingAbstractClass
        {
            [MetaMember(2)] public int DerivedClassIntField { get; set; }
        }

        [MetaFormDeprecated]
        [MetaSerializableDerived(5)]
        public class FormTestingDeprecatedClass : FormTestingAbstractClass
        {
            [MetaMember(2)] public int DerivedClassIntField { get; set; }
        }

        [MetaSerializable]
        public class FormTestingAttributes
        {
            class CustomIntFieldValidator : MetaFormValidator<int>
            {
                public override void Validate(int fieldOrForm, FormValidationContext ctx)
                {
                    if (fieldOrForm < -1 || fieldOrForm > 1)
                    {
                        ctx.Fail("Value must be within -1 and 1");
                    }
                }
            }

            [MetaFormDisplayProps("Attribute Given Name", DisplayHint = "Hello this is a hint!", DisplayPlaceholder = "Guess what number I'm thinking of...")]
            [MetaMember(1)] public int IntTest { get; set; }
            [MetaFormFieldCustomValidator(typeof(CustomIntFieldValidator))]
            [MetaMember(2)] public int ValidatedIntField { get; set; } = 1;
            [MetaFormFieldContext("CustomFieldContext", true)]
            [MetaMember(3)] public int CustomFieldContextTest { get; set; }
            [MetaFormDontCaptureDefault]
            [MetaMember(4)] public int DontCaptureDefaultTest { get; set; } = 1;

            [MetaMember(5)] public int CaptureDefaultNormallyTest { get; set; } = 1;
            [MetaFormRange(-5, 5, 0.1)]
            [MetaMember(6)] public double RangeTest { get; set; } = 1.0;
            [MetaFormTextArea]
            [MetaMember(7)] public string TextAreaTest { get; set; }
        }
    }
}
