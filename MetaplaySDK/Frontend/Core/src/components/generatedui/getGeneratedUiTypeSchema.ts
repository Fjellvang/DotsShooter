import { useGameServerApi } from '@metaplay/game-server-api'

import { useCoreStore } from '../../coreStore'
import { sleep } from '../../coreUtils'
import { EGeneratedUiTypeKind, type IGeneratedUiFieldTypeSchema } from './generatedUiTypes'
import { newtonsoftCamelCase } from './generatedUiUtils'

/**
 * Returns schema data from the server for the given type. Note that this function
 * uses a cache: typeSchemas. Entries in the cache have three states:
 * - Uncached. Has no entry in the cache.
 * - Loading. Has a null value in the cache.
 * - Cached. Has an object value in the cache.
 * @param typeName Name of the schema to load.
 * @returns Scheme data object.
 */
export async function GetTypeSchemaForTypeName(typeName: string): Promise<IGeneratedUiFieldTypeSchema> {
  const gameServerApi = useGameServerApi()
  const coreStore = useCoreStore()

  // Is the schema cached?
  if (typeName in coreStore.schemas) {
    // Already an entry in the cache.
    while (coreStore.schemas[typeName] === null) {
      // Wait until the schema is loaded (ie: not still loading)
      await sleep(10)
    }

    // Return data from the cache.

    return coreStore.schemas[typeName]
  } else {
    // Not in the cache. Set schema data to 'null' in the cache to mark it as 'loading'.
    coreStore.setSchemaForType(typeName, null)

    // Load the schema and store the data in the cache.
    try {
      const schema = (await gameServerApi.get(`forms/schema/${typeName}`)).data as IGeneratedUiFieldTypeSchema
      coreStore.setSchemaForType(typeName, schema)
    } catch (err: any) {
      throw new Error(`Failed to load schema for ${typeName} from the server! Reason: ${err.message}.`)
    }

    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    return coreStore.schemas[typeName]!
  }
}

export async function PreloadAllSchemasForTypeName(typeName: string): Promise<IGeneratedUiFieldTypeSchema> {
  return await GetTypeSchemaForTypeName(typeName)
}

/**
 * Strip any non MetaMember fields from an object
 */
export async function stripNonMetaFields(obj: any, schema: IGeneratedUiFieldTypeSchema): Promise<any> {
  if (schema.typeKind === EGeneratedUiTypeKind.Abstract) {
    const abstractType = obj?.$type

    if (abstractType) {
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      const abstractSchema = await GetTypeSchemaForTypeName(abstractType)
      return await stripNonMetaFields(obj, abstractSchema)
    }
  }
  if (!obj || typeof obj !== 'object' || !schema.fields) {
    return obj
  }

  const newObject: any = {}
  newObject.$type = obj.$type

  for (const field of schema.fields) {
    const camelField = newtonsoftCamelCase(field.fieldName)
    const fieldValue = obj[camelField]

    if (
      field.typeKind === EGeneratedUiTypeKind.Class ||
      field.typeKind === EGeneratedUiTypeKind.Abstract ||
      field.typeKind === EGeneratedUiTypeKind.Localized
    ) {
      const fieldSchema = await GetTypeSchemaForTypeName(field.fieldType)
      newObject[camelField] = await stripNonMetaFields(fieldValue, fieldSchema)
    } else if (field.typeKind === EGeneratedUiTypeKind.ValueCollection) {
      if (!field.typeParams) {
        throw new Error('ValueCollection must have typeParams')
      }
      const collectionSchema = await GetTypeSchemaForTypeName(field.typeParams[0])
      if (
        fieldValue &&
        (collectionSchema.typeKind === EGeneratedUiTypeKind.Class ||
          collectionSchema.typeKind === EGeneratedUiTypeKind.Abstract)
      ) {
        newObject[camelField] = await Promise.all(
          fieldValue.map(async (o: any) => await stripNonMetaFields(o, collectionSchema))
        )
      } else {
        newObject[camelField] = fieldValue
      }
    } else if (field.typeKind === EGeneratedUiTypeKind.KeyValueCollection) {
      if (!field.typeParams) {
        throw new Error('ValueCollection must have typeParams')
      }
      const collectionSchema = await GetTypeSchemaForTypeName(field.typeParams[1])
      if (
        fieldValue &&
        (collectionSchema.typeKind === EGeneratedUiTypeKind.Class ||
          collectionSchema.typeKind === EGeneratedUiTypeKind.Abstract)
      ) {
        newObject[camelField] = Object.fromEntries(
          await Promise.all(
            // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
            Object.entries(fieldValue).map(async ([k, v]) => [k, await stripNonMetaFields(v, collectionSchema)])
          )
        )
      } else {
        newObject[camelField] = fieldValue
      }
    } else {
      newObject[camelField] = fieldValue
    }
  }

  return newObject
}
