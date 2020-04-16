﻿using System;
using System.IO;
using System.Linq;

namespace VulkanGen
{
    class Program
    {
        static void Main(string[] args)
        {
            string vkFile = "..\\..\\..\\..\\..\\..\\KhronosRegistry\\vk.xml";
            string outputPath = "..\\..\\..\\..\\WaveEngine.Bindings.Vulkan\\Generated";

            var vulkanSpec = VulkanSpecification.FromFile(vkFile);

            // Write Constants
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Constants.cs")))
            {
                file.WriteLine("namespace WaveEngine.Bindings.Vulkan");
                file.WriteLine("{");
                file.WriteLine("\tpublic static partial class Vulkan");
                file.WriteLine("\t{");

                foreach (var constant in vulkanSpec.Constants)
                {
                    if (constant.Alias != null)
                    {
                        var refConstant = vulkanSpec.Constants.FirstOrDefault(c => c.Name == constant.Alias);
                        file.WriteLine($"\t\tpublic const {refConstant.Type.ToCSharp()} {constant.Name} = {refConstant.Name};");
                    }
                    else
                    {
                        file.WriteLine($"\t\tpublic const {constant.Type.ToCSharp()} {constant.Name} = {ConstantDefinition.NormalizeValue(constant.Value)};");
                    }
                }

                file.WriteLine("\t}");
                file.WriteLine("}");
            }

            // Delegates
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Delegates.cs")))
            {
                file.WriteLine("using System;\n");
                file.WriteLine("namespace WaveEngine.Bindings.Vulkan");
                file.WriteLine("{");

                foreach (var func in vulkanSpec.FuncPointers)
                {
                    file.Write($"\tpublic unsafe delegate {func.Type} {func.Name}(");
                    if (func.Parameters.Count > 0)
                    {
                        file.Write("\n");
                        string type, convertedType;

                        for (int p = 0; p < func.Parameters.Count; p++)
                        {
                            if (p > 0)
                                file.Write(",\n");

                            type = func.Parameters[p].Type;
                            var typeDef = vulkanSpec.TypeDefs.Find(t => t.Name == type);
                            if (typeDef != null)
                            {
                                vulkanSpec.BaseTypes.TryGetValue(typeDef.Type, out type);
                            }

                            convertedType = Helpers.ConvertBasicTypes(type);
                            if (convertedType == string.Empty)
                            {
                                convertedType = type;
                            }

                            file.Write($"\t\t{convertedType} {Helpers.ValidatedName(func.Parameters[p].Name)}");
                        }
                    }
                    file.Write(");\n\n");
                }

                file.WriteLine("}");
            }

            // Enums
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Enums.cs")))
            {
                file.WriteLine("using System;\n");
                file.WriteLine("namespace WaveEngine.Bindings.Vulkan");
                file.WriteLine("{");

                foreach (var e in vulkanSpec.Enums)
                {
                    if (e.Type == EnumType.Bitmask)
                        file.WriteLine("\t[Flags]");

                    file.WriteLine($"\tpublic enum {e.Name}");
                    file.WriteLine("\t{");

                    if (e.Values.Count == 0)
                    {
                        file.WriteLine("\t\tNone = 0,");
                    }
                    else
                    {
                        foreach (var member in e.Values)
                        {
                            file.WriteLine($"\t\t{member.Name} = {member.Value},");
                        }
                    }

                    file.WriteLine("\t}\n");

                }

                file.WriteLine("}");
            }

            // Unions
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Unions.cs")))
            {
                file.WriteLine("using System.Runtime.InteropServices;\n");
                file.WriteLine("namespace WaveEngine.Bindings.Vulkan");
                file.WriteLine("{");

                foreach (var union in vulkanSpec.Unions)
                {
                    file.WriteLine("\t[StructLayout(LayoutKind.Explicit)]");
                    file.WriteLine($"\tpublic unsafe partial struct {union.Name}");
                    file.WriteLine("\t{");
                    foreach (var member in union.Members)
                    {
                        string csType = Helpers.ConvertToCSharpType(member, vulkanSpec);

                        file.WriteLine($"\t\t[FieldOffset(0)]");
                        if (member.ElementCount > 1)
                        {
                            file.WriteLine($"\t\tpublic unsafe fixed {csType} {member.Name}[{member.ElementCount}];");
                        }
                        else
                        {
                            file.WriteLine($"\t\tpublic {csType} {member.Name};");
                        }
                    }

                    file.WriteLine("\t}\n");
                }

                file.WriteLine("}\n");
            }

            // structs
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Structs.cs")))
            {
                file.WriteLine("using System;");
                file.WriteLine("using System.Runtime.InteropServices;\n");
                file.WriteLine("namespace WaveEngine.Bindings.Vulkan");
                file.WriteLine("{");

                foreach (var structure in vulkanSpec.Structs)
                {
                    file.WriteLine("\t[StructLayout(LayoutKind.Sequential)]");
                    file.WriteLine($"\tpublic unsafe partial struct {structure.Name}");
                    file.WriteLine("\t{");
                    foreach (var member in structure.Members)
                    {
                        string csType = Helpers.ConvertToCSharpType(member, vulkanSpec);

                        if (member.ElementCount > 1)
                        {
                            for (int i = 0; i < member.ElementCount; i++)
                            {
                                file.WriteLine($"\t\tpublic {csType} {member.Name}_{i};");
                            }
                        }
                        else
                        {
                            file.WriteLine($"\t\tpublic {csType} {Helpers.ValidatedName(member.Name)};");
                        }
                    }

                    file.WriteLine("\t}\n");
                }

                file.WriteLine("}\n");
            }

            // Handles
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Handles.cs")))
            {
                file.WriteLine("using System;\n");
                file.WriteLine("namespace WaveEngine.Bindings.Vulkan");
                file.WriteLine("{");

                foreach (var handle in vulkanSpec.Handles)
                {
                    file.WriteLine($"\tpublic partial struct {handle.Name} : IEquatable<{handle.Name}>");
                    file.WriteLine("{");
                    string handleType = handle.Dispatchable ? "IntPtr" : "ulong";
                    string nullValue = handle.Dispatchable ? "IntPtr.Zero" : "0";

                    file.WriteLine($"\t\tpublic readonly {handleType} Handle;");

                    file.WriteLine($"\t\tpublic {handle.Name}({handleType} existingHandle) {{ Handle = existingHandle; }}");
                    file.WriteLine($"\t\tpublic static {handle.Name} Null => new {handle.Name}({nullValue});");
                    file.WriteLine($"\t\tpublic static implicit operator {handle.Name}({handleType} handle) => new {handle.Name}(handle);");
                    file.WriteLine($"\t\tpublic static bool operator ==({handle.Name} left, {handle.Name} right) => left.Handle == right.Handle;");
                    file.WriteLine($"\t\tpublic static bool operator !=({handle.Name} left, {handle.Name} right) => left.Handle != right.Handle;");
                    file.WriteLine($"\t\tpublic static bool operator ==({handle.Name} left, {handleType} right) => left.Handle == right;");
                    file.WriteLine($"\t\tpublic static bool operator !=({handle.Name} left, {handleType} right) => left.Handle != right;");
                    file.WriteLine($"\t\tpublic bool Equals({handle.Name} h) => Handle == h.Handle;");
                    file.WriteLine($"\t\tpublic override bool Equals(object o) => o is {handle.Name} h && Equals(h);");
                    file.WriteLine($"\t\tpublic override int GetHashCode() => Handle.GetHashCode();");
                    file.WriteLine("}\n");
                }

                file.WriteLine("}");
            }

            // Commands
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Commands.cs")))
            {
                file.WriteLine("using System;");
                file.WriteLine("using System.Runtime.InteropServices;\n");
                file.WriteLine("namespace WaveEngine.Bindings.Vulkan");
                file.WriteLine("{");
                file.WriteLine("\tpublic static unsafe partial class VulkanNative");
                file.WriteLine("\t{");

                foreach (var command in vulkanSpec.Commands)
                {
                    if (command.Alias != null)
                        continue;

                    string type, convertedType;
                    type = command.Prototype.Type;

                    vulkanSpec.BaseTypes.TryGetValue(type, out string baseType);
                    if (baseType != null)
                    {
                        type = baseType;
                    }
                    else
                    {
                        var typeDef = vulkanSpec.TypeDefs.Find(t => t.Name == type);
                        if (typeDef != null)
                        {
                            vulkanSpec.BaseTypes.TryGetValue(typeDef.Type, out type);
                        }
                    }

                    convertedType = Helpers.ConvertBasicTypes(type);
                    if (convertedType == string.Empty)
                    {
                        convertedType = type;
                    }

                    file.WriteLine("\t\t[UnmanagedFunctionPointer(CallingConvention.StdCall)]");

                    // private delegate void glTexBuffer_t(TextureTarget target, InternalFormat internalformat, uint buffer);
                    // Delegate
                    file.WriteLine($"\t\tprivate delegate {convertedType} {command.Prototype.Name}Delegate({command.GetParametersSignature(vulkanSpec)});");

                    // private static glTexBuffer_t p_glTexBuffer;
                    // internal function
                    file.WriteLine($"\t\tprivate static {command.Prototype.Name}Delegate {command.Prototype.Name}_ptr;");

                    // public static void glTexBuffer(TextureTarget target, InternalFormat internalformat, uint buffer) => p_glTexBuffer(target, internalformat, buffer);
                    // public function
                    file.WriteLine($"\t\tpublic static {convertedType} {command.Prototype.Name}({command.GetParametersSignature(vulkanSpec)})");
                    file.WriteLine($"\t\t\t=> {command.Prototype.Name}_ptr({command.GetParametersSignatureWithoutTypes()});\n");
                }

                file.WriteLine("\t}");
                file.WriteLine("}");
            }
        }
    }
}
