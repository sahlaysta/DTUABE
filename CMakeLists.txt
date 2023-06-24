cmake_minimum_required(VERSION 3.18)

project(DTUABE LANGUAGES CSharp)

include(CSharpUtilities)

add_executable(DTUABE
    DTUABEProjectEditor.cs
    DTUABEProjectJsonUtil.cs
    IlPatcher.cs
    IlStringsEditor.cs
    ProcessUtil.cs
    Program.cs
    UabeAssetsEditor.cs
)

set_property(TARGET DTUABE PROPERTY VS_DOTNET_TARGET_FRAMEWORK_VERSION "v4.6.1")

set(CMAKE_CSharp_FLAGS "/langversion:latest")

option(SHLYST_BUILD64BIT "Build 64 bit")

if(SHLYST_BUILD64BIT)
    set(CMAKE_CSharp_FLAGS "/platform:x64")
else()
    set(CMAKE_CSharp_FLAGS "/platform:x86")
endif()

set_property(TARGET DTUABE PROPERTY VS_DOTNET_REFERENCES
    "Microsoft.CSharp"
    "System"
    "System.Core"
    "System.Data"
    "System.Drawing"
)

set_property(TARGET DTUABE
    PROPERTY VS_PACKAGE_REFERENCES "System.ValueTuple_4.5.0"
    PROPERTY VS_PACKAGE_REFERENCES "Newtonsoft.Json_9.0.1"
)

set_target_properties(DTUABE PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/Sahlaysta.DTUABE$<IF:1,,>")

include(ExternalProject)
add_custom_command(TARGET DTUABE POST_BUILD
    COMMAND "${CMAKE_COMMAND}" -E copy_directory "${CMAKE_CURRENT_LIST_DIR}/IL" "${CMAKE_CURRENT_BINARY_DIR}/Sahlaysta.DTUABE/IL"
)
if(SHLYST_BUILD64BIT)
    add_custom_command(TARGET DTUABE POST_BUILD
        COMMAND "${CMAKE_COMMAND}" -E copy_directory "${CMAKE_CURRENT_LIST_DIR}/AutoUABE/AutoUABE64bit" "${CMAKE_CURRENT_BINARY_DIR}/Sahlaysta.DTUABE/AutoUABE"
    )
else()
    add_custom_command(TARGET DTUABE POST_BUILD
        COMMAND "${CMAKE_COMMAND}" -E copy_directory "${CMAKE_CURRENT_LIST_DIR}/AutoUABE/AutoUABE32bit" "${CMAKE_CURRENT_BINARY_DIR}/Sahlaysta.DTUABE/AutoUABE"
    )
endif()
