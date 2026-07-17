exports.preTransform = function (model) {
  model._disableToc = false;
  return model;
}

exports.postTransform = function (model) {
  return model;
}
