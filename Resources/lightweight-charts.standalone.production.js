
var LightweightCharts=(function(){function e(e,t){t=t||{};var n=t.width||e.clientWidth,r=t.height||e.clientHeight,o=document.createElement("canvas");o.width=n,o.height=r,e.appendChild(o);var i=o.getContext("2d"),a=[],l={addCandlestickSeries:function(){return{setData:function(e){a=e,s()}}},applyOptions:function(e){e=e||{},o.width=e.width||o.width,o.height=e.height||o.height,s()}};function s(){if(a.length){i.clearRect(0,0,o.width,o.height);var e=Math.max.apply(Math,a.map(function(e){return e.high})),t=Math.min.apply(Math,a.map(function(e){return e.low})),n=o.width/a.length;a.forEach(function(r,a){var l=r.open,u=r.close,c=r.high,h=r.low,d=a*n,f=(1-(l-t)/(e-t))*o.height,p=(1-(u-t)/(e-t))*o.height,w=(1-(c-t)/(e-t))*o.height,g=(1-(h-t)/(e-t))*o.height;i.strokeStyle=u>=l?"green":"red",i.beginPath(),i.moveTo(d+n/2,w),i.lineTo(d+n/2,g),i.stroke();var v=Math.min(f,p),m=Math.abs(f-p);i.fillStyle=i.strokeStyle,i.fillRect(d,v,n-1,m||1)})}}return l}return{createChart:e}})();

// Placeholder for Lightweight Charts library.
// TODO: Replace with full `lightweight-charts.standalone.production.js` contents.
var LightweightCharts = {
    createChart: function(container, options) {
        return {
            addCandlestickSeries: function() {
                return { setData: function() {} };
            },
            applyOptions: function() {}
        };
    }
};

