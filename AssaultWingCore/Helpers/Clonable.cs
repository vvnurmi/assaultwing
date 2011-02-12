﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using AW2.Core;
using AW2.Helpers.Serialization;

namespace AW2.Helpers
{
    /// <summary>
    /// A clonable entity. Supports cloning in respect of the <see cref="TypeParameterAttribute"/>
    /// limitation attribute.
    /// </summary>
    /// Subclasses only need to mark their fields with <see cref="TypeParameterAttribute"/> to
    /// support cloning.
    public abstract class Clonable
    {
        [TypeParameter, RuntimeState]
        private CanonicalString typeName;

        private delegate void CloneDelegate(Clonable clone);
        private delegate Clonable ConstructorDelegate();
        private static Dictionary<Type, DynamicMethod> g_cloneMethods = new Dictionary<Type, DynamicMethod>();
        private static Dictionary<Type, DynamicMethod> g_cloneMethodsWithRuntimeState = new Dictionary<Type, DynamicMethod>();
        private static Dictionary<Type, DynamicMethod> g_constructors = new Dictionary<Type, DynamicMethod>();
        private List<CloneDelegate> _cloneDelegates;
        private List<CloneDelegate> _cloneDelegatesWithRuntimeState;
        private ConstructorDelegate _constructorDelegate;

        public CanonicalString TypeName { get { return typeName; } }

        static Clonable()
        {
            Log.Write("Checking Clonable subclass constructors...");
            foreach (var type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!typeof(Clonable).IsAssignableFrom(type)) continue;
                if (type.IsAbstract) continue;
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance;
                if (null == type.GetConstructor(flags, null, Type.EmptyTypes, null))
                {
                    string message = "BUG: Missing constructor " + type.Name + "()";
                    Log.Write(message);
                    throw new ApplicationException(message);
                }
                if (null == type.GetConstructor(flags, null, new Type[] { typeof(CanonicalString) }, null))
                {
                    string message = "BUG: Missing constructor " + type.Name + "(CanonicalString)";
                    Log.Write(message);
                    throw new ApplicationException(message);
                }
            }
            Log.Write("...Clonable subclass constructors are OK");

            CreateCloneMethods();
        }

        /// <summary>
        /// For serialization only.
        /// </summary>
        /// In their parameterless constructors, subclasses should initialise
        /// all their fields marked with any limitation attribute, setting their
        /// values to representative defaults for XML templates.
        public Clonable()
        {
            typeName = (CanonicalString)"unknown type";
        }

        /// <summary>
        /// Creates an instance of the specified type. Common game logic should call
        /// <see cref="CreateGob(string, Action&lt;Gob&gt;)"/> instead of this method
        /// to create gobs.
        /// </summary>
        public static Clonable Instantiate(CanonicalString typeName)
        {
            var template = (Clonable)AssaultWingCore.Instance.DataEngine.GetTypeTemplate(typeName);
            return template.Clone();
        }

        /// <summary>
        /// Creates an instance of the specified type.
        /// </summary>
        /// The object's serialised fields are initialised according to the template instance
        /// associated with the specified type. This applies also to fields declared
        /// in subclasses, so a subclass constructor must not initialise its fields
        /// marked with <see cref="TypeDefinitionAttribute"/>.
        protected Clonable(CanonicalString typeName)
        {
            this.typeName = typeName;
        }

        /// <summary>
        /// Called on a cloned object after the cloning.
        /// </summary>
        public virtual void Cloned() { }

        public Clonable Clone()
        {
            return CloneImpl(ref _cloneDelegates, g_cloneMethods);
        }

        public Clonable CloneWithRuntimeState()
        {
            return CloneImpl(ref _cloneDelegatesWithRuntimeState, g_cloneMethodsWithRuntimeState);
        }

        private Clonable CloneImpl(ref List<CloneDelegate> cloneDelegates, Dictionary<Type, DynamicMethod> cloneMethods)
        {
            if (cloneDelegates == null)
            {
                cloneDelegates = new List<CloneDelegate>();
                for (var klass = GetType(); klass != null && cloneMethods.ContainsKey(klass); klass = klass.BaseType)
                    cloneDelegates.Add((CloneDelegate)cloneMethods[klass].CreateDelegate(typeof(CloneDelegate), this));
            }
            if (_constructorDelegate == null)
                _constructorDelegate = (ConstructorDelegate)g_constructors[GetType()].CreateDelegate(typeof(ConstructorDelegate), this);
            var clone = _constructorDelegate();
            for (int i = 0; i < cloneDelegates.Count; ++i) cloneDelegates[i](clone);
            clone.Cloned();
            return clone;
        }

        private static void CreateCloneMethods()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!typeof(Clonable).IsAssignableFrom(type) || type == typeof(Clonable)) continue;
                g_cloneMethods[type] = CreateCloneMethod(type, typeof(TypeParameterAttribute));
                g_cloneMethodsWithRuntimeState[type] = CreateCloneMethod(type, typeof(TypeParameterAttribute), typeof(RuntimeStateAttribute));
                g_constructors[type] = CreateConstructor(type);
            }
        }

        private static DynamicMethod CreateConstructor(Type type)
        {
            Type returnType = typeof(Clonable);
            Type[] parameterTypes = { typeof(Clonable) };
            var dyna = new DynamicMethod("ConstructorInvoker", returnType, parameterTypes, typeof(Clonable));
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var constructor = type.GetConstructor(flags, null, new Type[] { typeof(CanonicalString) }, null);
            var gob_GetTypeName = typeof(AW2.Game.Gob).GetProperty("TypeName").GetGetMethod(); // HACK: Reference to Gob
            var generator = dyna.GetILGenerator();

            // HACK: get value of ((Gob)this).TypeName
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, gob_GetTypeName);

            // call constructor and return its return value
            generator.Emit(OpCodes.Newobj, constructor);
            generator.Emit(OpCodes.Ret);
            return dyna;
        }

        private static DynamicMethod CreateCloneMethod(Type type, params Type[] cloneFieldAttributes)
        {
            Type returnType = null;
            Type[] parameterTypes = { typeof(Clonable), typeof(Clonable) };
            var dyna = new DynamicMethod("Clone", returnType, parameterTypes, type);
            var generator = dyna.GetILGenerator();
            var deepCopyMethod = typeof(AW2.Helpers.Serialization.Serialization).GetMethod("DeepCopy");

            // copy all fields from this to parameter
            var fields = cloneFieldAttributes.SelectMany(att => AW2.Helpers.Serialization.Serialization.GetDeclaredFields(type, att, null));
            foreach (var field in fields) EmitFieldCopyIL(generator, deepCopyMethod, field);
            generator.Emit(OpCodes.Ret);
            return dyna;
        }

        private static void EmitFieldCopyIL(ILGenerator generator, MethodInfo deepCopyMethod, FieldInfo field)
        {
            if (field.FieldType.IsValueType || field.IsDefined(typeof(ShallowCopyAttribute), false))
            {
                // shallow copy - copy value by simple assignment
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldfld, field);
                generator.Emit(OpCodes.Stfld, field);
            }
            else
            {
                // deep copy - call special method for obtaining a deep copy
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldfld, field);
                generator.Emit(OpCodes.Call, deepCopyMethod);
                generator.Emit(OpCodes.Stfld, field);
            }
        }
    }
}