grammar Vba;

// Placeholder grammar for VBA parsing.
// Lexical and expression rules will be added in the vba-lexical-and-expression-grammar child session set (#3).

// Start rule
module : EOF;

// Lexer rules
WS : [ \t\r\n]+ -> skip;
