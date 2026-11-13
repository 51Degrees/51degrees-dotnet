cmake_minimum_required(VERSION 3.18)
project(PerformanceTest)

include(ExternalProject)

set(EXTERNAL_INSTALL_LOCATION ${CMAKE_BINARY_DIR}/external)

ExternalProject_Add(ApacheBench
    GIT_REPOSITORY https://github.com/51degrees/apachebench
    CMAKE_ARGS -DCMAKE_INSTALL_PREFIX=${EXTERNAL_INSTALL_LOCATION}
    GIT_TAG origin/main
    STEP_TARGETS build
    EXCLUDE_FROM_ALL TRUE
)


# The user agents are the data repository's file, which its script downloads
# when it is missing.
set(DATA_DIR ${CMAKE_CURRENT_LIST_DIR}/../../cxx/data)
set(UAS_NAME "20000 User Agents.csv")
if(NOT EXISTS "${DATA_DIR}/device-detection/${UAS_NAME}")
    if(WIN32)
        execute_process(COMMAND powershell -NoProfile -ExecutionPolicy Bypass
            -File ${DATA_DIR}/get-data-files.ps1 ${UAS_NAME})
    else()
        execute_process(COMMAND sh ${DATA_DIR}/get-data-files.sh ${UAS_NAME})
    endif()
endif()
configure_file("${DATA_DIR}/device-detection/${UAS_NAME}" uas.csv COPYONLY)

add_custom_target(perf ALL
    COMMAND ${CMAKE_COMMAND} -E copy ${CMAKE_SOURCE_DIR}/runPerf.ps1 ./
    COMMAND ${CMAKE_COMMAND} -E copy ${CMAKE_SOURCE_DIR}/runPerf.sh ./
    DEPENDS ${CMAKE_SOURCE_DIR}/runPerf.ps1 ${CMAKE_SOURCE_DIR}/runPerf.sh

    VERBATIM
)

add_dependencies(perf ApacheBench-build)
