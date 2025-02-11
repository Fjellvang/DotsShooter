import{d as A,aa as x,f as P,C as h,M as F,b as v,o as t,e as k,w as d,k as y,n as c,a as s,p as f,t as o,l as m,F as S,r as B,$ as M,_ as z}from"./index-B7jvAjdE.js";import{r as C}from"./utils-Dwlepb3M.js";const D=A({__name:"MetaRawData",props:{kvPair:{},name:{}},setup(O,{expose:a}){a();const p=O,r=x(),_=P([]),w=h(()=>p.kvPair,(i,n)=>{if(!n&&i){for(const l of Object.keys(i))C(i[l])<800&&_.value.push(l);w()}},{deep:!1});function u(...i){const n=i.toString();return _.value.includes(n)}function g(...i){const n=i.toString();_.value.includes(n)?_.value=_.value.filter(l=>l!==n):_.value.push(n)}function b(i){return i.length>5?`[ ${i.slice(0,5).map(l=>`${typeof l=="object"?"object":l},`).join(" ")} ... ] (${i.length})`:i.length>0?`[ ${i.map(l=>`${typeof l=="object"?"object":l}`).join(", ")} ] (${i.length})`:"Array (0)"}function j(i){const n=Object.keys(i);return n.length>5?`{ ${n.slice(0,5).join(", ")}, ... } (${n.length})`:n.length>0?`{ ${n.join(", ")} } (${n.length})`:"Object (0)"}const e={props:p,uiStore:r,openProps:_,unwatch:w,isPropOpen:u,toggleProp:g,renderArray:b,renderObject:j,ref:P,watch:h,get MBadge(){return F},get useUiStore(){return x},get roughSizeOfObject(){return C}};return Object.defineProperty(e,"__isScriptSetup",{enumerable:!1,value:!0}),e}}),N={key:0,class:"tw-ml-1"},R={key:0},U={key:0,class:"tw-divide-y tw-divide-neutral-200 tw-text-xs"},E={key:0,class:"ml-2 text-muted small"},I={key:0},L={key:1},T={key:2},q={key:3},G={key:0},H={key:1},J={key:2},Q={key:4},W={key:5},X={key:6},Y={key:0},Z={key:1,class:"pl-2"},$=["onClick"],K={key:0,class:"ml-2 text-muted small"},V={key:0},ee={key:1},ne={key:2},te={key:3},se={key:0},ie={key:1},ae={key:2},re={key:4},oe={key:5},le={key:6},de={key:0,class:"pl-2"},ce={key:2},pe={key:2},_e={key:3};function fe(O,a,p,r,_,w){const u=v("fa-icon"),g=v("b-row"),b=v("b-alert"),j=v("b-col");return r.uiStore.showDeveloperUi?(t(),k(g,{key:0,class:"justify-content-center mt-5"},{default:d(()=>[y(j,{lg:"10"},{default:d(()=>[c("div",null,[y(u,{class:"mr-2 icon",icon:"code"}),a[1]||(a[1]=c("span",null,"Raw data",-1)),p.name?(t(),s("span",N,[a[0]||(a[0]=c("span",null,"for",-1)),y(r.MBadge,{class:"tw-ml-1"},{default:d(()=>[f("'"+o(p.name)+"'",1)],void 0,!0),_:1})])):m("",!0),a[2]||(a[2]=c("span",null,[c("span",{class:"small text-muted d-inline-block tw-ml-1"},"(enabled in developer mode)")],-1))]),p.kvPair!=null?(t(),s("div",R,[Object.keys(p.kvPair).length>0?(t(),s("ul",U,[(t(!0),s(S,null,B(p.kvPair,(e,i)=>(t(),s("li",{class:"pl-0 pr-0 pb-0 pb-2 text-monospace tw-break-all tw-py-3",key:i},[y(g,{class:M(["pointer text-break",{"not-collapsed":r.isPropOpen(i)}]),"no-gutters":"","align-v":"center",onClick:n=>r.toggleProp(String(i))},{default:d(()=>[y(u,{class:"tw-mr-1",icon:"angle-right",size:"1x"}),c("span",null,o(i),1),r.isPropOpen(i)?m("",!0):(t(),s("small",E,[e===null?(t(),s("span",I,"null")):Array.isArray(e)?(t(),s("span",L,o(r.renderArray(e)),1)):typeof e=="object"?(t(),s("span",T,o(r.renderObject(e)),1)):typeof e=="string"?(t(),s("span",q,[e?e.length>40?(t(),s("span",H,o(e.slice(0,40))+"...",1)):(t(),s("span",J,o(e),1)):(t(),s("span",G,"empty string"))])):typeof e=="boolean"?(t(),s("span",Q,[c("span",null,o(e),1)])):typeof e=="number"?(t(),s("span",W,o(e),1)):(t(),s("span",X,o(typeof e),1))]))],void 0,!0),_:2},1032,["class","onClick"]),r.isPropOpen(i)?(t(),s("div",Y,[e==null?(t(),k(r.MBadge,{key:0,class:"tw-mb-4"},{default:d(()=>a[3]||(a[3]=[f("null")]),void 0,!0),_:1})):typeof e=="object"?(t(),s("div",Z,[(t(!0),s(S,null,B(Object.keys(e),n=>(t(),s("div",{key:n,style:{"margin-bottom":"0.1rem"}},[c("div",{class:M(["pointer",{"not-collapsed":r.isPropOpen(i,n)}]),onClick:l=>r.toggleProp(i,n)},[y(u,{class:"tw-mr-1",icon:"angle-right",size:"1x"}),f(o(n),1),r.isPropOpen(i,n)?m("",!0):(t(),s("span",K,[e[n]===null?(t(),s("span",V,"null")):Array.isArray(e[n])?(t(),s("span",ee,o(r.renderArray(e[n])),1)):typeof e[n]=="object"?(t(),s("span",ne,o(r.renderObject(e[n])),1)):typeof e[n]=="string"?(t(),s("span",te,[e[n]?e[n].length>40?(t(),s("span",ie,o(e[n].slice(0,40))+"...",1)):(t(),s("span",ae,o(e[n]),1)):(t(),s("span",se,"empty string"))])):typeof e[n]=="boolean"?(t(),s("span",re,[c("span",null,o(e[n]),1)])):typeof e[n]=="number"?(t(),s("span",oe,o(e[n]),1)):(t(),s("span",le,o(typeof e[n]),1))]))],10,$),r.isPropOpen(i,n)?(t(),s("div",de,[e[n]==null?(t(),k(r.MBadge,{key:0,class:"tw-mb-1"},{default:d(()=>a[4]||(a[4]=[f("null")]),void 0,!0),_:1})):e[n]===""?(t(),k(r.MBadge,{key:1,class:"tw-mb-1"},{default:d(()=>a[5]||(a[5]=[f("empty string")]),void 0,!0),_:1})):(t(),s("pre",ce,o(e[n]),1))])):m("",!0)]))),128))])):typeof e=="string"?(t(),s("span",pe,[e===""?(t(),k(r.MBadge,{key:0,class:"tw-mb-1"},{default:d(()=>a[6]||(a[6]=[f("empty string")]),void 0,!0),_:1})):m("",!0),c("pre",null,o(e),1)])):(t(),s("pre",_e,o(e),1))])):m("",!0)]))),128))])):(t(),k(b,{key:1,class:"mt-2 tw-text-center",show:"",variant:"secondary"},{default:d(()=>a[7]||(a[7]=[f("0 results")]),void 0,!0),_:1}))])):(t(),k(b,{key:1,class:"mt-2 tw-text-center",show:"",variant:"warning"},{default:d(()=>a[8]||(a[8]=[f("No data!")]),void 0,!0),_:1}))],void 0,!0),_:1})],void 0,!0),_:1})):m("",!0)}const ye=z(D,[["render",fe],["__scopeId","data-v-c4c986bc"],["__file","MetaRawData.vue"]]);export{ye as default};
