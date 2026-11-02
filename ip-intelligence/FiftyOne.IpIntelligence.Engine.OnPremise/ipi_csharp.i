%include "cxx/src/common/CsTypes.i"

// Pass the required property indexes as a pinned int[] with a count. A null
// array crosses as a null pointer and, with the count of -1 the wrapper
// supplies, means every graph.
%apply int INPUT[] {const int *requiredPropertyIndexes}

%include "cxx/src/ipi/ipi.i"
