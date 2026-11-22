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


# ApacheBench sends the User-Agents of uas.csv, one on each line, which are
# those of the records of the data repository's YAML evidence file. Its
# script downloads the file when it is missing. A value YAML had to quote is
# in single quotes, with each quote inside it doubled.
set(DATA_DIR ${CMAKE_CURRENT_LIST_DIR}/../../cxx/data)
set(EVIDENCE_NAME "20000 Evidence Records.yml")
set(EVIDENCE_FILE "${DATA_DIR}/device-detection/${EVIDENCE_NAME}")
if(NOT EXISTS "${EVIDENCE_FILE}")
    if(WIN32)
        execute_process(COMMAND powershell -NoProfile -ExecutionPolicy Bypass
            -File ${DATA_DIR}/get-data-files.ps1 ${EVIDENCE_NAME})
    else()
        execute_process(COMMAND sh ${DATA_DIR}/get-data-files.sh ${EVIDENCE_NAME})
    endif()
endif()
file(STRINGS "${EVIDENCE_FILE}" USER_AGENT_LINES REGEX "^header\\.user-agent: ")
file(WRITE ${CMAKE_CURRENT_BINARY_DIR}/uas.csv "")
foreach(line IN LISTS USER_AGENT_LINES)
    string(REGEX REPLACE "^header\\.user-agent: " "" line "${line}")
    if(line MATCHES "^'(.*)'$")
        set(line "${CMAKE_MATCH_1}")
        string(REPLACE "''" "'" line "${line}")
    endif()
    file(APPEND ${CMAKE_CURRENT_BINARY_DIR}/uas.csv "${line}\n")
endforeach()

add_custom_target(perf ALL
    COMMAND ${CMAKE_COMMAND} -E copy ${CMAKE_SOURCE_DIR}/runPerf.ps1 ./
    COMMAND ${CMAKE_COMMAND} -E copy ${CMAKE_SOURCE_DIR}/runPerf.sh ./
    DEPENDS ${CMAKE_SOURCE_DIR}/runPerf.ps1 ${CMAKE_SOURCE_DIR}/runPerf.sh

    VERBATIM
)

add_dependencies(perf ApacheBench-build)
