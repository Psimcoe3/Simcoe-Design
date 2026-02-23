using System.Reflection;
using System.Runtime.Loader;
using System.IO;

namespace ElectricalComponentSandbox.Services.RevitIntrospection;

public interface IRevitManagedAssemblyInspector
{
    IReadOnlyList<ManagedAssemblyMetadata> InspectAssemblies(
        IReadOnlyList<RevitBinaryInfo> binaries,
        RevitIntrospectionOptions options);
}

public sealed class RevitManagedAssemblyInspector : IRevitManagedAssemblyInspector
{
    public IReadOnlyList<ManagedAssemblyMetadata> InspectAssemblies(
        IReadOnlyList<RevitBinaryInfo> binaries,
        RevitIntrospectionOptions options)
    {
        var managedBinaries = binaries
            .Where(binary => binary.Exists && binary.Classification == RevitBinaryClassification.ManagedAssembly)
            .ToList();

        var output = new List<ManagedAssemblyMetadata>(managedBinaries.Count);
        foreach (var binary in managedBinaries)
            output.Add(InspectAssembly(binary, options));

        return output;
    }

    private static ManagedAssemblyMetadata InspectAssembly(RevitBinaryInfo binary, RevitIntrospectionOptions options)
    {
        var assemblyDirectory = Path.GetDirectoryName(binary.FullPath) ?? string.Empty;
        var context = new MetadataOnlyAssemblyLoadContext(assemblyDirectory);

        try
        {
            var assembly = context.LoadFromAssemblyPath(binary.FullPath);
            var types = SafeGetExportedTypes(assembly);

            var inspectedTypes = new List<ManagedTypeMetadata>();
            foreach (var type in types.Take(options.MaxTypesPerAssembly))
            {
                inspectedTypes.Add(new ManagedTypeMetadata(
                    Namespace: type.Namespace ?? string.Empty,
                    FullTypeName: type.FullName ?? type.Name,
                    Methods: SafeGetMethodSignatures(type, options.MaxMembersPerType),
                    Properties: SafeGetPropertySignatures(type, options.MaxMembersPerType)));
            }

            return new ManagedAssemblyMetadata
            {
                FileName = binary.FileName,
                FullPath = binary.FullPath,
                AssemblyName = assembly.GetName().Name ?? binary.FileName,
                AssemblyVersion = assembly.GetName().Version?.ToString(),
                ExportedTypeCount = types.Length,
                Types = inspectedTypes
            };
        }
        catch (Exception ex)
        {
            return new ManagedAssemblyMetadata
            {
                FileName = binary.FileName,
                FullPath = binary.FullPath,
                AssemblyName = Path.GetFileNameWithoutExtension(binary.FileName),
                AssemblyVersion = null,
                InspectionError = ex.GetType().Name + ": " + ex.Message,
                ExportedTypeCount = 0,
                Types = Array.Empty<ManagedTypeMetadata>()
            };
        }
        finally
        {
            context.Unload();
        }
    }

    private static Type[] SafeGetExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null).Cast<Type>().ToArray();
        }
    }

    private static IReadOnlyList<string> SafeGetMethodSignatures(Type type, int maxMembers)
    {
        try
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Take(maxMembers)
                .Select(BuildMethodSignature)
                .Where(signature => !string.IsNullOrWhiteSpace(signature))
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> SafeGetPropertySignatures(Type type, int maxMembers)
    {
        try
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Take(maxMembers)
                .Select(BuildPropertySignature)
                .Where(signature => !string.IsNullOrWhiteSpace(signature))
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string BuildMethodSignature(MethodInfo method)
    {
        try
        {
            var parameters = method.GetParameters()
                .Select(parameter => ToFriendlyTypeName(parameter.ParameterType))
                .ToArray();
            var parameterText = string.Join(", ", parameters);
            var declaringType = method.DeclaringType?.FullName ?? "<unknown>";
            var returnType = ToFriendlyTypeName(method.ReturnType);
            return $"{declaringType}.{method.Name}({parameterText}) : {returnType}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildPropertySignature(PropertyInfo property)
    {
        try
        {
            var declaringType = property.DeclaringType?.FullName ?? "<unknown>";
            var propertyType = ToFriendlyTypeName(property.PropertyType);
            return $"{declaringType}.{property.Name} : {propertyType}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ToFriendlyTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var genericName = type.Name;
        var tick = genericName.IndexOf('`');
        if (tick > 0)
            genericName = genericName[..tick];

        var args = type.GetGenericArguments().Select(ToFriendlyTypeName);
        return $"{genericName}<{string.Join(", ", args)}>";
    }

    private sealed class MetadataOnlyAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly string _assemblyDirectory;

        public MetadataOnlyAssemblyLoadContext(string assemblyDirectory)
            : base(nameof(MetadataOnlyAssemblyLoadContext), isCollectible: true)
        {
            _assemblyDirectory = assemblyDirectory;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName.Name))
                return null;

            var localPath = Path.Combine(_assemblyDirectory, assemblyName.Name + ".dll");
            if (!File.Exists(localPath))
                return null;

            return LoadFromAssemblyPath(localPath);
        }
    }
}
