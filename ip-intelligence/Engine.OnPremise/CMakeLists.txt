cmake_minimum_required(VERSION 3.24)

set (CMAKE_C_STANDARD 11)
set (CMAKE_CXX_STANDARD 11)

# Include the C API
project(IpIntelligenceEngine VERSION 4.3.0 LANGUAGES CXX C)

# Position independent code, which the shared library needs, for every
# target. CMake 4.5 and later report add_compile_options and the other
# directory-wide directives as an author warning, which is an error here, so
# the settings below use variables instead.
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if (MSVC)
    if (CMAKE_GENERATOR_PLATFORM)
        set (GEN_PLATFORM ${CMAKE_GENERATOR_PLATFORM})
    else()
        set (GEN_PLATFORM ${CMAKE_VS_PLATFORM_NAME_DEFAULT})
    endif()

    if (${GEN_PLATFORM} MATCHES .*64.* AND NOT _51DEGREES_32BIT)
        set(ARCH x64)
    else()
        set(ARCH x86)
    endif()

else()
    message(STATUS "System Processor: ${CMAKE_SYSTEM_PROCESSOR}")
    if (${CMAKE_SYSTEM_PROCESSOR} MATCHES ^armhf)
        set(ARCH armhf)
    elseif (${CMAKE_SYSTEM_PROCESSOR} MATCHES ^aarch64)
        set(ARCH aarch64)
    elseif (${CMAKE_SYSTEM_PROCESSOR} MATCHES ^arm64)
        set(ARCH aarch64)
    elseif (${CMAKE_SYSTEM_PROCESSOR} MATCHES 64)
        if (_51DEGREES_32BIT)
            message(STATUS "_51DEGREES_32BIT: ${_51DEGREES_32BIT}")
            set (ARCH x86)
        else()
            message(STATUS "_51DEGREES_32BIT: ${_51DEGREES_32BIT}")
            set (ARCH x64)
        endif()
    else()
        set (ARCH x86)
    endif()
endif()
message(STATUS "Arch name: ${ARCH}")
message(STATUS "Generator Platform: ${CMAKE_GENERATOR_PLATFORM}")

if (MSVC)
    set (OS windows)
elseif (UNIX AND NOT APPLE)
    set (OS linux)
elseif (APPLE)
    set (OS macos)
endif()
message(STATUS "OS name: ${OS}")

# The C and C++ code is the cxx submodule at the repository root. Only IP
# Intelligence is built here.
set(_51DEGREES_BUILD_DEVICE_DETECTION OFF)

# Every target, the cxx libraries included, warns at the level the cxx tests
# use. When the _51DEGREES_WARNINGS_AS_ERRORS environment variable is "true",
# in any case, as CI sets it, every warning is an error, on each compiler and
# linker, and the configure step makes CMake's own warnings errors with the
# arguments ci/cmake-warning-args.ps1 writes. Otherwise, as in a build from
# source by anyone else, nothing here makes a warning an error.
string(TOLOWER "$ENV{_51DEGREES_WARNINGS_AS_ERRORS}" FIFTYONE_WARNINGS_AS_ERRORS)
if (MSVC)
    set(FIFTYONE_WARNINGS " /W4")
    set(FIFTYONE_WARNINGS_ERROR "/WX")
    set(FIFTYONE_LINK_WARNINGS " /WX")
elseif (APPLE)
    set(FIFTYONE_WARNINGS " -Wall -Wextra")
    set(FIFTYONE_WARNINGS_ERROR "-Werror")
    set(FIFTYONE_LINK_WARNINGS " -Wl,-fatal_warnings")
else()
    set(FIFTYONE_WARNINGS " -Wall -Wextra")
    set(FIFTYONE_WARNINGS_ERROR "-Werror")
    set(FIFTYONE_LINK_WARNINGS " -Wl,--fatal-warnings")
endif()
string(APPEND CMAKE_C_FLAGS "${FIFTYONE_WARNINGS}")
string(APPEND CMAKE_CXX_FLAGS "${FIFTYONE_WARNINGS}")
if (FIFTYONE_WARNINGS_AS_ERRORS STREQUAL "true")
    set(CMAKE_COMPILE_WARNING_AS_ERROR ON)
    string(APPEND CMAKE_EXE_LINKER_FLAGS "${FIFTYONE_LINK_WARNINGS}")
    string(APPEND CMAKE_SHARED_LINKER_FLAGS "${FIFTYONE_LINK_WARNINGS}")
    string(APPEND CMAKE_MODULE_LINKER_FLAGS "${FIFTYONE_LINK_WARNINGS}")
    if (MSVC)
        string(APPEND CMAKE_STATIC_LINKER_FLAGS " /WX")
    endif()
endif()
include(${CMAKE_CURRENT_LIST_DIR}/../../cxx/CMakeLists.txt NO_POLICY_SCOPE)

if (RebuildSwig)
    find_package(SWIG 4.0 COMPONENTS csharp)
    if (NOT SWIG_FOUND)
        message(WARNING "SWIG cannot be found so will not be run")
    elseif (NOT SWIG_csharp_FOUND)
        message(WARNING "SWIG does not include csharp support so will not be run")
    else()
        message(STATUS "Rebuilding SWIG interfaces")
        set(SRCOUT ${CMAKE_CURRENT_LIST_DIR}/Interop/Swig)
        execute_process(COMMAND ${SWIG_EXECUTABLE} -c++ -csharp -I${CMAKE_CURRENT_LIST_DIR}/../.. -namespace FiftyOne.IpIntelligence.Engine.OnPremise.Interop -module IpIntelligenceEngineModule -dllimport FiftyOne.IpIntelligence.Engine.OnPremise.Native.dll -outdir ${CMAKE_CURRENT_LIST_DIR}/Interop/Swig -o ${CMAKE_CURRENT_LIST_DIR}/IpIntelligenceEngineSwig_csharp.cpp ${CMAKE_CURRENT_LIST_DIR}/ipi_csharp.i)
    endif()
endif()

add_library(fiftyone-ipi-dotnet SHARED
    ${CMAKE_CURRENT_LIST_DIR}/IpIntelligenceEngineSwig_csharp.cpp)

if(UNIX)
    set_target_properties(fiftyone-ipi-dotnet
        PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "."
        LIBRARY_OUTPUT_DIRECTORY "./${CMAKE_BUILD_TYPE}"
        PREFIX ""
        OUTPUT_NAME "FiftyOne.IpIntelligence.Engine.OnPremise.Native"
        SUFFIX ".dll"
    )
else()
    set_target_properties(fiftyone-ipi-dotnet
        PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "."
        LIBRARY_OUTPUT_DIRECTORY "."
        OUTPUT_NAME "FiftyOne.IpIntelligence.Engine.OnPremise.Native"
        SUFFIX ".dll"
    )
endif()

# The wrapper includes through cxx/, so the repository root is on its include path.
target_include_directories(fiftyone-ipi-dotnet PRIVATE ${CMAKE_CURRENT_LIST_DIR}/../..)
target_link_libraries(fiftyone-ipi-dotnet fiftyone-ip-intelligence-cxx)
# These macros stop the SWIG wrapper from turning compiler warnings off.
target_compile_definitions(fiftyone-ipi-dotnet PRIVATE
    SWIG_MSC_UNSUPPRESS_4505
    SWIG_NO_CRT_SECURE_NO_DEPRECATE
    SWIG_NO_SCL_SECURE_NO_DEPRECATE)
if (MSVC)
    target_compile_options(fiftyone-ipi-dotnet PRIVATE "/W4")
else()
    target_compile_options(fiftyone-ipi-dotnet PRIVATE "-Wall" "-Wextra")
    target_compile_options(fiftyone-ipi-dotnet INTERFACE "-static-libgcc -static-libstdc++")
    target_link_options(fiftyone-ipi-dotnet INTERFACE "-static-libgcc -static-libstdc++")
endif()
# The wrapper's warnings are errors under the same switch as above.
if (FIFTYONE_WARNINGS_AS_ERRORS STREQUAL "true")
    set_target_properties(fiftyone-ipi-dotnet PROPERTIES COMPILE_WARNING_AS_ERROR ON)
    target_compile_options(fiftyone-ipi-dotnet PRIVATE "${FIFTYONE_WARNINGS_ERROR}")
    if (MSVC)
        target_link_options(fiftyone-ipi-dotnet PRIVATE "/WX")
    endif()
else()
    set_target_properties(fiftyone-ipi-dotnet PROPERTIES COMPILE_WARNING_AS_ERROR OFF)
endif()
