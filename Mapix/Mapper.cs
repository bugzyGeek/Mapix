using System;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using static Mapix.DataAnnotation.Mapper;

namespace Mapix
{
    /// <summary>
    /// A utility class that can map one type to another using reflection.
    /// </summary>
    public partial class Mapper
    {
        // Declare a static concurrent dictionary to store the MapperReflection objects for different types
        static readonly ConcurrentDictionary<Type, MapperReflection> s_objectProperties = new ConcurrentDictionary<Type, MapperReflection>();
        // Declare a static method info to store the reference to the Map method
        static readonly MethodInfo s_map = typeof(Mapper).GetMethod(nameof(Map), BindingFlags.Static | BindingFlags.NonPublic)!;
        // Declare a static concurrent dictionary to store the object activators of different types
        static readonly ConcurrentDictionary<Type, Func<object>> s_objectActivators = new ConcurrentDictionary<Type, Func<object>>();
        // Declare a constant string to store the name of a string type
        const string s_stringType  = "String";
        // Declare a constant string to store the name of the method to add items to a collection.
        const string s_collectionAdd = "Add";

        /// <summary>
        /// Maps the properties of a model object to a target object of a different type.
        /// </summary>
        /// <typeparam name="I">The type of the input object.</typeparam>
        /// <typeparam name="O">The type of the outpit object.</typeparam>
        /// <param name="input">The model object to be mapped.</param>
        /// <param name="output">The target object to be mapped to. If null, a new instance of the target type will be created.</param>
        /// <returns>The output object with the mapped properties.</returns>
        public static O Map<I, O>(I input, O output)
        where I : class
        where O : class
        {
            // Make the generic method of the Map method with the property types
            MethodInfo mappingGeneric = s_map.MakeGenericMethod(typeof(I), typeof(O));
            // Invoke the Map method with the value and the mapped objects dictionary
            return mappingGeneric?.Invoke(null, new object[] { input, output }) as O;
        }

        /// <summary>
        /// Maps the properties of a model object to a target object of a different type.
        /// </summary>
        /// <typeparam name="T">The type of the model object.</typeparam>
        /// <typeparam name="M">The type of the target object.</typeparam>
        /// <param name="model">The model object to be mapped.</param>
        /// <param name="target">The target object to be mapped to. If null, a new instance of the target type will be created.</param>
        /// <param name="mappedObjects">A dictionary of mapped objects to avoid mapping the same object twice or creating circular references.</param>
        /// <returns>The target object with the mapped properties.</returns>
        private static M Map<T, M>(T model, M target = null, Dictionary<int, object> mappedObjects = null)
        where T : class
        where M : class
        {
            // Check if the model is null and return null if so
            if (model is null) 
                return null;

            // Check if the model is of the same type as the target and return the model if so
            if (model is M)
                return model as M;

            // Initialize the mapped objects dictionary if it is null
            mappedObjects ??= new Dictionary<int, object>();
            // Get a unique hash code for the model object
            int modelHash = RuntimeHelpers.GetHashCode(model);

            // Check if the model is already mapped and return the mapped object if so
            if (mappedObjects.ContainsKey(modelHash))
                return mappedObjects[modelHash] as M;

            // Get or create the MapperReflection object for the model type
            MapperReflection modelReflection = s_objectProperties.GetOrAdd(typeof(T), type => new MapperReflection(type));
            // Get or create the MapperReflection object for the target type
            MapperReflection targetReflection = s_objectProperties.GetOrAdd(typeof(M), type => new MapperReflection(type));

            // Create a new instance of the target type if it is null
            target ??= CreateInstance<M>();
            // Add the model hash and the target to the mapped objects dictionary
            mappedObjects.Add(modelHash, target);

            // Loop through the properties of the model type
            foreach (var property in modelReflection.PropertiesInfo)
            {
                // Get the custom attribute of the property that indicates whether to ignore the mapping
                IgnoreMapAttribute ignoreMap = property.GetCustomAttribute<IgnoreMapAttribute>(true);
                // Check if the ignore map attribute is not null and continue if so
                if (ignoreMap != null) 
                    continue;

                // Get the value of the property from the model
                object value = property.GetValue(model);

                // Get the custom attribute of the property that specifies the name of the matching property in the target type
                MapToAttribute mapTo = property.GetCustomAttribute<MapToAttribute>(true);
                // Find the matching property in the target type by name or by the map to attribute
                var p = targetReflection.PropertiesInfo.FirstOrDefault(x => x.Name == (mapTo?.Name ?? property.Name));

                // Check if the matching property is null or the value is null and continue if so
                if (p is null || value is null) 
                    continue;

                // Use nameof operator instead of hard-coded name of IEnumerable interface
                // Check if the property is an enumerable type and not a string
                if (property.PropertyType.GetInterface(nameof(IEnumerable)) != null && !property.PropertyType.Name.Equals("String"))
                    // Call the IEnumerableProperty method to handle the mapping of the enumerable property
                    IEnumerableProperty(property, p, value!, target, mappedObjects);
                // Check if the property is a class type and not a string
                else if (property.PropertyType.IsClass && !property.PropertyType.Name.Equals("String"))
                    // Call the ClassProperty method to handle the mapping of the class property
                    ClassProperty(property, p, value, target, mappedObjects);
                // Otherwise, set the value of the matching property in the target
                else 
                    p.SetValue(target, value);
            }
            // Return the target object
            return target!;
        }

        /// <summary>
        /// Handles the mapping of an enumerable property from one type to another.
        /// </summary>
        /// <typeparam name="M">The type of the target object.</typeparam>
        /// <param name="property">The property of the model type.</param>
        /// <param name="p">The matching property of the target type.</param>
        /// <param name="value">The value of the property from the model object.</param>
        /// <param name="target">The target object to be mapped to.</param>
        /// <param name="mappedObjects">A dictionary of mapped objects to avoid mapping the same object twice or creating circular references.</param>
        static void IEnumerableProperty<M>(PropertyInfo property, PropertyInfo p, object value, M target, Dictionary<int, object> mappedObjects = null) where M : class
        {
            // Get a unique hash code for the value object
            int valueHash = RuntimeHelpers.GetHashCode(value);
            // Check if the value is already mapped and set the matching property in the target if so
            if (mappedObjects.ContainsKey(valueHash))
            {
                p.SetValue(target, mappedObjects[valueHash]);
                return;
            }
            // Check if the value is of the same type as the target and set the matching property in the target if so
            if (value is M)
            {
                p.SetValue(target, value);
                return;
            }

            // Use var keyword instead of explicit type declaration
            // Get the generic type argument of the target enumerable type
            var enumerableOfTypeTarget = p.PropertyType.GetGenericArguments()[0];
            // Get the generic difinition type of the target enumerable
            var collectionDefinitionOfTarget = p.PropertyType.GetGenericTypeDefinition();
            // Make a generic collection of type target with the generic type argument
            var targetEnumerableType = collectionDefinitionOfTarget.MakeGenericType(enumerableOfTypeTarget);
            var targetCollection = CreateInstance(targetEnumerableType);

            // Get the generic type argument of the model enumerable type
            var ofType = property.PropertyType.GetGenericArguments()[0];
            // Make the generic method of the Map method with the generic type arguments
            MethodInfo mappingGeneric = s_map.MakeGenericMethod(ofType, enumerableOfTypeTarget);
            // Cast the value to an IEnumerable interface
            IEnumerable enumerableCollection = (IEnumerable)value;

            // Cast the elements of the enumerable type to the target type and create a new instance of the target enumerable type
            targetCollection = enumerableCollection.Cast<object>().Select(element =>
            {
                // Check if the element is null and return null if so
                if (element is null) 
                    return null;
                // Check if the element is a class type and not a string
                if (element.GetType().IsClass && !property.PropertyType.Name.Equals("String"))
                    // Invoke the Map method with the element and the mapped objects dictionary
                    return mappingGeneric.Invoke(null, new object[] { element, null, mappedObjects });
                // Otherwise, return the element
                else 
                    return element;
            });
            // Add the value hash and the target enumerable type to the mapped objects dictionary
            mappedObjects.Add(valueHash, targetCollection);
            // Set the matching property in the target with the target enumerable type
            p.SetValue(target, targetCollection);
        }

        /// <summary>
        /// Handles the mapping of a class property from one type to another.
        /// </summary>
        /// <typeparam name="M">The type of the target object.</typeparam>
        /// <param name="modelProperty">The property of the model type.</param>
        /// <param name="castProperty">The matching property of the target type.</param>
        /// <param name="value">The value of the property from the model object.</param>
        /// <param name="target">The target object to be mapped to.</param>
        /// <param name="mappedObjects">A dictionary of mapped objects to avoid mapping the same object twice or creating circular references.</param>
        static void ClassProperty<M>(PropertyInfo modelProperty, PropertyInfo castProperty, object value, M target, Dictionary<int, object> mappedObjects = null)
        {
            // Get a unique hash code for the value object
            int valueHash = RuntimeHelpers.GetHashCode(value);
            // Check if the value is already mapped and set the matching property in the target if so
            if (mappedObjects.ContainsKey(valueHash))
            {
                castProperty.SetValue(target, mappedObjects[valueHash]);
                return;
            }
            // Check if the value is of the same type as the target and set the matching property in the target if so
            if (value is M)
            {
                castProperty.SetValue(target, value);
                return;
            }
            // Make the generic method of the Map method with the property types
            MethodInfo mappingGeneric = s_map.MakeGenericMethod(modelProperty.PropertyType, castProperty.PropertyType);
            // Invoke the Map method with the value and the mapped objects dictionary
            dynamic task = mappingGeneric?.Invoke(null, new object[] { value, null, mappedObjects });
            // Set the matching property in the target with the mapped value
            castProperty.SetValue(target, task);
        }

        // Define a generic method to create an instance of a given type
        static T CreateInstance<T>()
        {
            // Use ConcurrentDictionary.GetOrAdd method instead of TryGetValue and TryAdd methods
            // Get or create the object activator for the type
            var activator = s_objectActivators.GetOrAdd(typeof(T), type =>
            {
                // Get the parameterless constructor of the type
                ConstructorInfo ctor = typeof(T).GetConstructor(Type.EmptyTypes);
                // Create a lambda expression that invokes the constructor
                var lambda = Expression.Lambda<Func<object>>(Expression.New(ctor));
                // Compile the lambda expression into a delegate
                return lambda.Compile();
            });

            return (T)activator();
        }
        // Define a method to create an instance of a given type
        static object CreateInstance(Type type)
        {
            // Use ConcurrentDictionary.GetOrAdd method instead of TryGetValue and TryAdd methods
            // Get or create the object activator for the type
            var activator = s_objectActivators.GetOrAdd(type, type =>
            {
                // Get the parameterless constructor of the type
                var ctor = type.GetConstructor(Type.EmptyTypes);
                // Create a lambda expression that invokes the constructor
                var lambda = Expression.Lambda<Func<object>>(Expression.New(ctor));
                // Compile the lambda expression into a delegate
                return lambda.Compile();
            });
            return activator();
        }
    }
}
