﻿// 2015-02-06 [LTL] - object for iframe

var Iframe = Class.create({
    initialize: function (options) {
        "use strict";
        this.finishedLoading = false;
        var div = $(options.div);
        div.src = "getHtml.ashx?" + $H({ frame: options.data.FrameId }).toQueryString();

        div.observe('load', function () {
            this.finishedLoading = true;
            //console.log("iframe " + options.div.id + " finished loading");
        }.bind(this));
    },

    isReady: function () {
        "use strict";
        return !!this.finishedLoading;
    },
});
