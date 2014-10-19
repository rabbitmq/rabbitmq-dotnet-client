/* ***************************************************************************
This is the C language version of NameMapper.py.  See the comments and
DocStrings in NameMapper for details on the purpose and interface of this
module.

===============================================================================
$Id: _namemapper.c,v 1.31 2005/01/06 15:13:16 tavis_rudd Exp $
Authors: Tavis Rudd <tavis@damnsimple.com>
Version: $Revision: 1.31 $
Start Date: 2001/08/07
Last Revision Date: $Date: 2005/01/06 15:13:16 $
*/

/* *************************************************************************** */
#include "Python.h"             /* Python header files */
#include <string.h>
#include <stdlib.h>

#ifdef __cplusplus
extern "C" {
#endif


static PyObject *NotFound;   /* locally-raised exception */
static PyObject *TooManyPeriods;   /* locally-raised exception */
static PyObject* pprintMod_pformat; /* used for exception formatting */
#define MAXCHUNKS 15		/* max num of nameChunks for the arrays */
#define TRUE 1
#define FALSE 0

#define ALLOW_WRAPPING_OF_NOTFOUND_EXCEPTIONS 1
#define INCLUDE_NAMESPACE_REPR_IN_NOTFOUND_EXCEPTIONS 0

# define createNameCopyAndChunks() {\
  nameCopy = malloc(strlen(name) + 1);\
  tmpPntr1 = name; \
  tmpPntr2 = nameCopy;\
  while ((*tmpPntr2++ = *tmpPntr1++)); \
  numChunks = getNameChunks(nameChunks, name, nameCopy); \
  if (PyErr_Occurred()) { 	/* there might have been TooManyPeriods */\
    free(nameCopy);\
    return NULL;\
  }\
}

#define OLD_checkForNameInNameSpaceAndReturnIfFound() { \
    if ( PyNamemapper_hasKey(nameSpace, nameChunks[0]) ) {\
      theValue = PyNamemapper_valueForName(nameSpace, nameChunks, numChunks, executeCallables);\
      free(nameCopy);\
      if (wrapInternalNotFoundException(name, nameSpace)) {\
	theValue = NULL;\
      }\
      return theValue;\
    }\
}

#define checkForNameInNameSpaceAndReturnIfFound(namespace_decref) { \
    if ( PyNamemapper_hasKey(nameSpace, nameChunks[0]) ) {\
      theValue = PyNamemapper_valueForName(nameSpace, nameChunks, numChunks, executeCallables);\
      if (namespace_decref) {\
        Py_DECREF(nameSpace);\
      }\
      if (wrapInternalNotFoundException(name, nameSpace)) {\
	theValue = NULL;\
      }\
      goto done;\
    }\
}

/* *************************************************************************** */
/* First the c versions of the functions */
/* *************************************************************************** */

static void
setNotFoundException(char *key, PyObject *namespace)
{


  PyObject *exceptionStr = NULL;
  exceptionStr = Py_BuildValue("s","cannot find '");
  PyString_ConcatAndDel(&exceptionStr, Py_BuildValue("s", key));
  PyString_ConcatAndDel(&exceptionStr, Py_BuildValue("s", "'"));
  if (INCLUDE_NAMESPACE_REPR_IN_NOTFOUND_EXCEPTIONS) {
    PyString_ConcatAndDel(&exceptionStr, Py_BuildValue("s", " in the namespace "));
    PyString_ConcatAndDel(&exceptionStr, 
			  PyObject_CallFunctionObjArgs(pprintMod_pformat, namespace, NULL));
  }
  PyErr_SetObject(NotFound, exceptionStr);
  Py_DECREF(exceptionStr);
}

static int
wrapInternalNotFoundException(char *fullName, PyObject *namespace)
{
  PyObject *excType, *excValue, *excTraceback, *isAlreadyWrapped = NULL;
  if (!ALLOW_WRAPPING_OF_NOTFOUND_EXCEPTIONS) {
    return 0;
  } 
  if (PyErr_Occurred() && PyErr_GivenExceptionMatches(PyErr_Occurred(), NotFound)) {
    PyErr_Fetch(&excType, &excValue, &excTraceback);
    isAlreadyWrapped = PyObject_CallMethod(excValue, "find", "s", "while searching");
    if (PyInt_AsLong(isAlreadyWrapped)==-1) { /* only wrap once */
      PyString_ConcatAndDel(&excValue, Py_BuildValue("s", " while searching for '"));
      PyString_ConcatAndDel(&excValue, Py_BuildValue("s", fullName));
      PyString_ConcatAndDel(&excValue, Py_BuildValue("s", "'"));
      if (INCLUDE_NAMESPACE_REPR_IN_NOTFOUND_EXCEPTIONS) {
	PyString_ConcatAndDel(&excValue, Py_BuildValue("s", " in "));
	PyString_ConcatAndDel(&excValue, Py_BuildValue("s", "the top-level namespace "));
	PyString_ConcatAndDel(&excValue, 
			      PyObject_CallFunctionObjArgs(pprintMod_pformat, namespace, NULL));
      }
    }
    Py_DECREF(isAlreadyWrapped);
    PyErr_Restore(excType, excValue, excTraceback);
    return -1;
  } else {
    return 0;
  }
}

static int getNameChunks(char *nameChunks[], char *name, char *nameCopy) 
{
  char c;
  char *currChunk;
  int currChunkNum = 0;
  
  currChunk = nameCopy;
  while ('\0' != (c = *nameCopy)){
    if ('.' == c) {
      if (currChunkNum >= (MAXCHUNKS-2)) { /* avoid overflowing nameChunks[] */
	PyErr_SetString(TooManyPeriods, name); 
	return 0;
      }

      *nameCopy ='\0';
      nameChunks[currChunkNum++] = currChunk;
      nameCopy++;
      currChunk = nameCopy;
    } else 
      nameCopy++;
  }
  if (nameCopy > currChunk) {
    nameChunks[currChunkNum++] = currChunk;
  }
  return currChunkNum;
}


static int 
PyNamemapper_hasKey(PyObject *obj, char *key)
{
  if (PyMapping_Check(obj) && PyMapping_HasKeyString(obj, key)) {
    return TRUE;
  } else if (PyObject_HasAttrString(obj, key)) {
    return TRUE;
  } else {
    return FALSE;
  }
}


static PyObject *
PyNamemapper_valueForKey(PyObject *obj, char *key)
{
  PyObject *theValue = NULL;

  if (PyMapping_Check(obj) && PyMapping_HasKeyString(obj, key)) {
    theValue = PyMapping_GetItemString(obj, key);
  } else if (PyObject_HasAttrString(obj, key)) {
    theValue = PyObject_GetAttrString(obj, key);

  } else {
    setNotFoundException(key, obj);
  }

  return theValue;
}

static PyObject *
PyNamemapper_valueForName(PyObject *obj, char *nameChunks[], 
			  int numChunks, 
			  int executeCallables)
{
  int i;
  char *currentKey;
  PyObject *currentVal = NULL;
  PyObject *nextVal = NULL;

  currentVal = obj;
  for (i=0; i < numChunks;i++) {
    currentKey = nameChunks[i];
    if (PyErr_CheckSignals()) {	/* not sure if I really need to do this here, but what the hell */
      if (i>0) {
	Py_DECREF(currentVal);
      }
      return NULL;
    }
    
    if (PyMapping_Check(currentVal) && PyMapping_HasKeyString(currentVal, currentKey)) {
      nextVal = PyMapping_GetItemString(currentVal, currentKey);
    } else if (PyObject_HasAttrString(currentVal, currentKey)) {
      nextVal = PyObject_GetAttrString(currentVal, currentKey);
    } else {
      setNotFoundException(currentKey, currentVal);
      if (i>0) {
	Py_DECREF(currentVal);
      }
      return NULL;
    }
    if (i>0) {
      Py_DECREF(currentVal);
    }
    if (executeCallables && PyCallable_Check(nextVal) && (!PyInstance_Check(nextVal)) 
	&& (!PyClass_Check(nextVal)) && (!PyType_Check(nextVal)) ) {
      if (!(currentVal = PyObject_CallObject(nextVal, NULL))){
	Py_DECREF(nextVal);
	return NULL;
      };
      Py_DECREF(nextVal);
    } else {
      currentVal = nextVal;
    }
  }

  return currentVal;
}


/* *************************************************************************** */
/* Now the wrapper functions to export into the Python module */
/* *************************************************************************** */


static PyObject *
namemapper_valueForKey(PyObject *self, PyObject *args)
{
  PyObject *obj;
  char *key;

  if (!PyArg_ParseTuple(args, "Os", &obj, &key)) {
    return NULL;
  }

  return PyNamemapper_valueForKey(obj, key);

}

static PyObject *
namemapper_valueForName(PyObject *self, PyObject *args, PyObject *keywds)
{


  PyObject *obj;
  char *name;
  int executeCallables = 0;

  char *nameCopy = NULL;
  char *tmpPntr1 = NULL;
  char *tmpPntr2 = NULL;
  char *nameChunks[MAXCHUNKS];
  int numChunks;

  PyObject *theValue;

  static char *kwlist[] = {"obj", "name", "executeCallables", NULL};

  if (!PyArg_ParseTupleAndKeywords(args, keywds, "Os|i", kwlist,  &obj, &name, &executeCallables)) {
    return NULL;
  }

  createNameCopyAndChunks();  

  theValue = PyNamemapper_valueForName(obj, nameChunks, numChunks, executeCallables);
  free(nameCopy);
  if (wrapInternalNotFoundException(name, obj)) {
    theValue = NULL;
  }
  return theValue;

}

static PyObject *
namemapper_valueFromSearchList(PyObject *self, PyObject *args, PyObject *keywds)
{

  PyObject *searchList;
  char *name;
  int executeCallables = 0;

  char *nameCopy = NULL;
  char *tmpPntr1 = NULL;
  char *tmpPntr2 = NULL;
  char *nameChunks[MAXCHUNKS];
  int numChunks;

  PyObject *nameSpace = NULL;
  PyObject *theValue = NULL;
  PyObject *iterator = NULL;

  static char *kwlist[] = {"searchList", "name", "executeCallables", NULL};

  if (!PyArg_ParseTupleAndKeywords(args, keywds, "Os|i", kwlist,  &searchList, &name, 
				   &executeCallables)) {
    return NULL;
  }

  createNameCopyAndChunks();

  iterator = PyObject_GetIter(searchList);
  if (iterator == NULL) {
    PyErr_SetString(PyExc_TypeError,"This searchList is not iterable!");
    goto done;
  }

  while ( (nameSpace = PyIter_Next(iterator)) ) {
    checkForNameInNameSpaceAndReturnIfFound(TRUE);
    Py_DECREF(nameSpace);
    if(PyErr_CheckSignals()) {
      theValue = NULL;
      goto done;
    }
  }
  if (PyErr_Occurred()) {
    theValue = NULL;
    goto done;
  }

  setNotFoundException(nameChunks[0], searchList);
 done:
  Py_XDECREF(iterator);
  free(nameCopy);
  return theValue;
}

static PyObject *
namemapper_valueFromFrameOrSearchList(PyObject *self, PyObject *args, PyObject *keywds)
{

  /* python function args */
  char *name;
  int executeCallables = 0;
  PyObject *searchList = NULL;

  /* locals */
  char *nameCopy = NULL;
  char *tmpPntr1 = NULL;
  char *tmpPntr2 = NULL;
  char *nameChunks[MAXCHUNKS];
  int numChunks;

  PyObject *nameSpace = NULL;
  PyObject *theValue = NULL;
  PyObject *excString = NULL;
  PyObject *iterator = NULL;

  static char *kwlist[] = {"searchList", "name", "executeCallables", NULL};

  if (!PyArg_ParseTupleAndKeywords(args, keywds, "Os|i", kwlist,  &searchList, &name, 
				   &executeCallables)) {
    return NULL;
  }

  createNameCopyAndChunks();
  
  nameSpace = PyEval_GetLocals();
  checkForNameInNameSpaceAndReturnIfFound(FALSE);  

  iterator = PyObject_GetIter(searchList);
  if (iterator == NULL) {
    PyErr_SetString(PyExc_TypeError,"This searchList is not iterable!");
    goto done;
  }
  while ( (nameSpace = PyIter_Next(iterator)) ) {
    checkForNameInNameSpaceAndReturnIfFound(TRUE);
    Py_DECREF(nameSpace);
    if(PyErr_CheckSignals()) {
      theValue = NULL;
      goto done;
    }
  }
  if (PyErr_Occurred()) {
    theValue = NULL;
    goto done;
  }

  nameSpace = PyEval_GetGlobals();
  checkForNameInNameSpaceAndReturnIfFound(FALSE);

  nameSpace = PyEval_GetBuiltins();
  checkForNameInNameSpaceAndReturnIfFound(FALSE);

  excString = Py_BuildValue("s", "[locals()]+searchList+[globals(), __builtins__]");
  setNotFoundException(nameChunks[0], excString);
  Py_DECREF(excString);

 done:
  Py_XDECREF(iterator);
  free(nameCopy);
  return theValue;
}

static PyObject *
namemapper_valueFromFrame(PyObject *self, PyObject *args, PyObject *keywds)
{

  /* python function args */
  char *name;
  int executeCallables = 0;

  /* locals */
  char *tmpPntr1 = NULL;
  char *tmpPntr2 = NULL;

  char *nameCopy = NULL;
  char *nameChunks[MAXCHUNKS];
  int numChunks;

  PyObject *nameSpace = NULL;
  PyObject *theValue = NULL;
  PyObject *excString = NULL;

  static char *kwlist[] = {"name", "executeCallables", NULL};

  if (!PyArg_ParseTupleAndKeywords(args, keywds, "s|i", kwlist, &name, &executeCallables)) {
    return NULL;
  }

  createNameCopyAndChunks();
  
  nameSpace = PyEval_GetLocals();
  checkForNameInNameSpaceAndReturnIfFound(FALSE);
  
  nameSpace = PyEval_GetGlobals();
  checkForNameInNameSpaceAndReturnIfFound(FALSE);

  nameSpace = PyEval_GetBuiltins();
  checkForNameInNameSpaceAndReturnIfFound(FALSE);

  excString = Py_BuildValue("s", "[locals(), globals(), __builtins__]");
  setNotFoundException(nameChunks[0], excString);
  Py_DECREF(excString);
 done:
  free(nameCopy);
  return theValue;
}

/* *************************************************************************** */
/* Method registration table: name-string -> function-pointer */

static struct PyMethodDef namemapper_methods[] = {
  {"valueForKey", namemapper_valueForKey,  1},
  {"valueForName", (PyCFunction)namemapper_valueForName,  METH_VARARGS|METH_KEYWORDS},
  {"valueFromSearchList", (PyCFunction)namemapper_valueFromSearchList,  METH_VARARGS|METH_KEYWORDS},
  {"valueFromFrame", (PyCFunction)namemapper_valueFromFrame,  METH_VARARGS|METH_KEYWORDS},
  {"valueFromFrameOrSearchList", (PyCFunction)namemapper_valueFromFrameOrSearchList,  METH_VARARGS|METH_KEYWORDS},
  {NULL,         NULL}
};


/* *************************************************************************** */
/* Initialization function (import-time) */

DL_EXPORT(void)
init_namemapper(void)
{
  PyObject *m, *d, *pprintMod;

  /* create the module and add the functions */
  m = Py_InitModule("_namemapper", namemapper_methods);        /* registration hook */
  
  /* add symbolic constants to the module */
  d = PyModule_GetDict(m);
  NotFound = PyErr_NewException("NameMapper.NotFound",PyExc_LookupError,NULL);
  TooManyPeriods = PyErr_NewException("NameMapper.TooManyPeriodsInName",NULL,NULL);
  PyDict_SetItemString(d, "NotFound", NotFound);
  PyDict_SetItemString(d, "TooManyPeriodsInName", TooManyPeriods);
  pprintMod = PyImport_ImportModule("pprint"); /* error check this */
  pprintMod_pformat = PyObject_GetAttrString(pprintMod, "pformat");
  Py_DECREF(pprintMod);
  /* check for errors */
  if (PyErr_Occurred())
    Py_FatalError("Can't initialize module _namemapper");
}

#ifdef __cplusplus
}
#endif
