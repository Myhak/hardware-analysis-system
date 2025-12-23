cmake_minimum_required(VERSION 3.15)
project(HardwareAnalysisSystem VERSION 1.0.0 LANGUAGES CXX)

# Требуем C++17
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Включаем все предупреждения
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall -Wextra -Wpedantic")

# Режимы сборки
if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release)
endif()

set(CMAKE_CXX_FLAGS_DEBUG "-g -O0 -DDEBUG")
set(CMAKE_CXX_FLAGS_RELEASE "-O3 -DNDEBUG -march=native")

# Опции для тестирования
option(BUILD_TESTS "Build unit tests" ON)
option(CODE_COVERAGE "Enable code coverage" OFF)

# Найти необходимые библиотеки
find_package(Threads REQUIRED)
find_package(Boost COMPONENTS system filesystem REQUIRED)

# ============================================================================
# Stage 2: Hardware Monitor (Low-level MSR access)
# ============================================================================

add_executable(stage2_hardware_monitor
    src/cpp/hardware_monitor.cpp
)

target_include_directories(stage2_hardware_monitor PRIVATE
    ${CMAKE_SOURCE_DIR}/src/cpp
)

target_link_libraries(stage2_hardware_monitor PRIVATE
    Threads::Threads
)

# ============================================================================
# Stage 4: Optimization Algorithms (будет добавлено позже)
# ============================================================================

# add_executable(stage4_optimization
#     src/cpp/optimization_engine.cpp
# )

# ============================================================================
# Stage 7: Hardware Integration (будет добавлено позже)
# ============================================================================

# add_executable(stage7_integration
#     src/cpp/hardware_integration.cpp
# )

# ============================================================================
# Unit Tests (Google Test)
# ============================================================================

if(BUILD_TESTS)
    enable_testing()
    
    # Найти или скачать Google Test
    include(FetchContent)
    FetchContent_Declare(
        googletest
        GIT_REPOSITORY https://github.com/google/googletest.git
        GIT_TAG v1.14.0
    )
    FetchContent_MakeAvailable(googletest)
    
    # Тесты для Stage 2
    add_executable(test_hardware_monitor
        tests/unit/cpp/test_hardware_monitor.cpp
        src/cpp/hardware_monitor.cpp
    )
    
    target_include_directories(test_hardware_monitor PRIVATE
        ${CMAKE_SOURCE_DIR}/src/cpp
    )
    
    target_link_libraries(test_hardware_monitor PRIVATE
        gtest_main
        Threads::Threads
    )
    
    include(GoogleTest)
    gtest_discover_tests(test_hardware_monitor)
endif()

# ============================================================================
# Code Coverage
# ============================================================================

if(CODE_COVERAGE AND CMAKE_BUILD_TYPE MATCHES Debug)
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} --coverage -fprofile-arcs -ftest-coverage")
    set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} --coverage")
    
    find_program(LCOV lcov REQUIRED)
    find_program(GENHTML genhtml REQUIRED)
    
    add_custom_target(coverage
        COMMAND ${LCOV} --directory . --capture --output-file coverage.info
        COMMAND ${LCOV} --remove coverage.info '/usr/*' --output-file coverage.info
        COMMAND ${LCOV} --list coverage.info
        COMMAND ${GENHTML} coverage.info --output-directory coverage_report
        WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
        COMMENT "Generating code coverage report"
    )
endif()

# ============================================================================
# Installation
# ============================================================================

install(TARGETS stage2_hardware_monitor
    RUNTIME DESTINATION bin
)

# ============================================================================
# Documentation (Doxygen)
# ============================================================================

find_package(Doxygen)
if(DOXYGEN_FOUND)
    set(DOXYGEN_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/docs/api)
    set(DOXYGEN_GENERATE_HTML YES)
    set(DOXYGEN_GENERATE_XML YES)
    set(DOXYGEN_EXTRACT_ALL YES)
    set(DOXYGEN_EXTRACT_PRIVATE YES)
    
    doxygen_add_docs(docs
        ${CMAKE_SOURCE_DIR}/src/cpp
        COMMENT "Generating API documentation with Doxygen"
    )
endif()

# ============================================================================
# Package Configuration
# ============================================================================

set(CPACK_PACKAGE_NAME "hardware-analysis-system")
set(CPACK_PACKAGE_VERSION "${PROJECT_VERSION}")
set(CPACK_PACKAGE_DESCRIPTION "Hardware Analysis and Optimization System")
set(CPACK_GENERATOR "DEB;TGZ")

include(CPack)

# ============================================================================
# Print Configuration Summary
# ============================================================================

message(STATUS "")
message(STATUS "========================================")
message(STATUS "Hardware Analysis System Configuration")
message(STATUS "========================================")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "C++ Compiler: ${CMAKE_CXX_COMPILER}")
message(STATUS "C++ Standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "Build tests: ${BUILD_TESTS}")
message(STATUS "Code coverage: ${CODE_COVERAGE}")
message(STATUS "========================================")
message(STATUS "")
