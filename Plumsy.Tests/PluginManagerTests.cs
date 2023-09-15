using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Plum.Net.Models;
using Plum.Net.Tests.Resolvers;
using Plum.Net.Tests.SimplePlugin;
using Plum.Net.Validators;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;

namespace Plum.Net.Tests;

[TestClass]
public class PluginManagerTests
{
    private readonly string pluginDirectory;
    private readonly PluginManager pluginManager;

    public PluginManagerTests()
    {
        this.pluginDirectory = Path.Combine(Environment.CurrentDirectory, "Plugins");
        this.pluginManager = new(this.pluginDirectory);
        this.pluginManager.WithDependencyResolver(new SimpleDependencyResolver());
    }

    [TestInitialize]
    public void TestInitialize()
    {
        var assemblyLoadEventHandlerField = typeof(AssemblyLoadContext).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).First(f => f.Name == "AssemblyLoad");
        assemblyLoadEventHandlerField.SetValue(AssemblyLoadContext.Default, null);

        var resolveDependencyEventHandlerField = typeof(AssemblyLoadContext).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).First(f => f.Name == "AssemblyResolve");
        resolveDependencyEventHandlerField.SetValue(AssemblyLoadContext.Default, null);
    }

    [TestMethod]
    public void PluginManager_MissingPluginDirectory_ShouldReturnNoPlugins()
    {
        var pluginManager = new PluginManager(Path.Combine(this.pluginDirectory, Guid.NewGuid().ToString()));

        var plugins = pluginManager.GetAvailablePlugins();

        plugins.Should().HaveCount(0);
    }

    [TestMethod]
    public void PluginManager_ExistingPlugin_ShouldReturnExpectedPlugin()
    {
        var plugins = this.pluginManager.GetAvailablePlugins();

        plugins.Should().HaveCount(1);
        plugins.First().Name.Should().Be("Plum.Net.Tests.SimplePlugin");
    }

    [TestMethod]
    public void PluginManager_VersionValidator_ValidatesVersion()
    {
        var called = false;
        var validator = Substitute.For<IEnvironmentVersionValidator>();
        this.pluginManager.WithEnvironmentVersionValidator(validator);
        validator.Validate(Arg.Any<Version>(), Arg.Any<Version>()).Returns(callinfo =>
        {
            called = true;
            return true;
        });

        var plugins = this.pluginManager.GetAvailablePlugins().ToList();

        called.Should().BeTrue();
    }

    [TestMethod]
    public void PluginManager_VersionValidator_CurrentVersionMatchesEnvironment()
    {
        var validator = Substitute.For<IEnvironmentVersionValidator>();
        this.pluginManager.WithEnvironmentVersionValidator(validator);
        validator.Validate(Arg.Any<Version>(), Arg.Any<Version>()).Returns(callinfo =>
        {
            var currentVersion = callinfo.ArgAt<Version>(0);
            currentVersion.Should().Be(Environment.Version);

            return true;
        });

        var plugins = this.pluginManager.GetAvailablePlugins().ToList();
    }

    [TestMethod]
    public void PluginManager_VersionValidatorDenies_ReturnsNoPlugins()
    {
        var validator = Substitute.For<IEnvironmentVersionValidator>();
        this.pluginManager.WithEnvironmentVersionValidator(validator);
        validator.Validate(Arg.Any<Version>(), Arg.Any<Version>()).Returns(callinfo =>
        {
            return false;
        });

        var plugins = this.pluginManager.GetAvailablePlugins().ToList();

        plugins.Should().HaveCount(0);
    }

    [TestMethod]
    public void PluginManager_TypeDefinitionsValidator_CallsValidator()
    {
        var called = false;
        var validator = Substitute.For<ITypeDefinitionsValidator>();
        this.pluginManager.WithTypeDefinitionsValidator(validator);
        validator.Validate(Arg.Any<IEnumerable<TypeDefinition>>(), Arg.Any<MetadataReader>()).Returns(callinfo =>
        {
            called = true;
            return true;
        });

        var plugins = this.pluginManager.GetAvailablePlugins().ToList();
        called.Should().BeTrue();
    }

    [TestMethod]
    public void PluginManager_TypeDefinitionsValidator_ReceivesMainType()
    {
        var validator = Substitute.For<ITypeDefinitionsValidator>();
        this.pluginManager.WithTypeDefinitionsValidator(validator);
        validator.Validate(Arg.Any<IEnumerable<TypeDefinition>>(), Arg.Any<MetadataReader>()).Returns(callinfo =>
        {
            var typeDefinitions = callinfo.ArgAt<IEnumerable<TypeDefinition>>(0);
            var metadataReader = callinfo.ArgAt<MetadataReader>(1);
            typeDefinitions.Select(t => metadataReader.GetString(t.Name)).Any(t => t == "Main").Should().BeTrue();
            return true;
        });

        var plugins = this.pluginManager.GetAvailablePlugins().ToList();
    }

    [TestMethod]
    public void PluginManager_MetadataValidator_CallsValidator()
    {
        var called = false;
        var validator = Substitute.For<IMetadataValidator>();
        this.pluginManager.WithMetadataValidator(validator);
        validator.Validate(Arg.Any<MetadataReader>()).Returns(callinfo =>
        {
            var metadataReader = callinfo.ArgAt<MetadataReader>(0);
            metadataReader.Should().NotBeNull();
            called = true;
            return true;
        });

        var plugins = this.pluginManager.GetAvailablePlugins().ToList();
        called.Should().BeTrue();
    }

    [TestMethod]
    public void PluginManager_LoadPlugins_ShouldSucceed()
    {
        var plugins = this.pluginManager.GetAvailablePlugins();

        var results = this.pluginManager.LoadPlugins(plugins);

        results.Should().HaveCount(1);
        results.First().Should().BeOfType<PluginLoadOperation.Success>();
    }

    [TestMethod]
    public void PluginManager_LoadPlugins_ReturnsExpectedAssembly()
    {
        this.pluginManager.WithForceLoadDependencies(true);
        var plugins = this.pluginManager.GetAvailablePlugins();

        var results = this.pluginManager.LoadPlugins(plugins);
        var assembly = results.OfType<PluginLoadOperation.Success>().First().Plugin.Assembly;
        var mainType = assembly.GetTypes().First(t => t.Name == nameof(Main));
        var main = Activator.CreateInstance(mainType).As<Main>();

        main.Should().NotBeNull();
        main.ReturnTrue().Should().BeTrue();
    }

    [TestMethod]
    [Ignore("This test fails when run together with the other tests, as the dependent assembly is loaded into the AppDomain by other tests")]
    public void PluginManager_LoadPlugins_WithNoResolver_FailsToResolveAssembly()
    {
        var pluginManager = new PluginManager(this.pluginDirectory);
        pluginManager.WithForceLoadDependencies(true);
        var plugins = pluginManager.GetAvailablePlugins();

        var results = pluginManager.LoadPlugins(plugins);

        results.First().Should().BeOfType<PluginLoadOperation.ExceptionEncountered>();
    }

    [TestMethod]
    public void PluginManager_LoadPlugins_WithNoResolverAndNoForcedDependencyResolve_Succeeds()
    {
        var pluginManager = new PluginManager(this.pluginDirectory);
        pluginManager.WithForceLoadDependencies(false);
        var plugins = pluginManager.GetAvailablePlugins();

        var results = pluginManager.LoadPlugins(plugins);

        results.First().Should().BeOfType<PluginLoadOperation.Success>();
    }
}