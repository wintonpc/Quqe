function makeChart(name, elementSelector, width, height) {
  return d3.select(elementSelector)
            .attr("class", name)
            .attr("width", width)
            .attr("height", height);
}

function addSubchart(chart, name, shape, data, style, attrs) {

  $("head").append('<style>.' + name + '{' + style + '}</style>');

  var c = chart.selectAll("." + name + " " + shape)
                .data(data)
                .enter().append(shape)
                .attr("class", name);

  for (var key in attrs) {
    c.attr(key, attrs[key]);
  }

  return c;
}

function getUri(uri, success, error) {
  $.ajax(uri, {
    dataType: "json",
    success: success,
    error: error
  });
}
