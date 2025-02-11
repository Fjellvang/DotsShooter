import{d as L,f as y,c as m,bG as b,C as I,dN as T,dO as z,o as p,e as D,w as F,n as g,a2 as u,a as O,t as _,l as N,_ as R}from"./index-B7jvAjdE.js";import{a as k}from"./utils-Dwlepb3M.js";const U=L({__name:"MetaInputSelect",props:{value:{},options:{type:[Array,Function]},disabled:{type:Boolean},required:{type:Boolean},multiselect:{type:Boolean},placeholder:{},noClear:{type:Boolean},searchFields:{},dataTestid:{}},emits:["input"],setup(n,{expose:f,emit:a}){f();const e=n,v=!Array.isArray(e.options),c=y([]);function o(t){const l=t.map(i=>i.label),s=new Set(l);l.length!==s.size&&console.error("Duplicate IDs found in options array of MetaInputSelect:",t)}const d=m(()=>{const t=e.options.map(l=>({label:l.id,value:l.value,disabled:l.disabled}));return o(t),t});async function w(t){const l=e.options,s=await l(t);c.value=s;const i=s.map(C=>({label:C.id,value:C.value,disabled:C.disabled}));return o(i),i}const M=m(()=>v?c.value.find(t=>b(t.value,e.value)):Array.isArray(e.value)?e.value.map(t=>d.value.find(l=>b(l.value,t))):d.value.find(t=>b(t.value,e.value)));function A(t){let l;return v?l=c.value.find(s=>s.id===t)?.value:l=e.options.find(s=>s.id===t)?.value,l}const r=y(),q=m(()=>typeof e.options=="function"?!0:e.options===void 0||e.options.length===0?!1:typeof e.options[0].value=="object"&&e.searchFields?!0:typeof e.options[0].value!="object");function V(t){r.value=t.toLocaleLowerCase()}function E(t){return typeof t.value=="object"?(e.searchFields??[]).some(l=>k(t.value,l).toString().toLowerCase().includes(r.value)):r.value?t.value.toString().toLowerCase().includes(r.value)||t.label.toString().toLowerCase().includes(r.value):!0}const S=a;function j(t){e.multiselect?S("input",t.map(l=>l.value)):S("input",t?.value)}const h=y();I(h,()=>{const t=h.value?.$el?.clientWidth,l=h.value?.$el?.children;if(t&&l){const s=Object.values(l).find(i=>i.className.includes("multiselect-dropdown"));if(s){const i=s;i.style.minWidth=`${t}px`}}});const B={props:e,isOptionsAFunction:v,cachedOptions:c,checkForUniqueIds:o,optionsForMultiSelectComponent:d,lazyOptionsForMultiSelectComponent:w,valueForMultiSelectComponent:M,getOptionValueById:A,searchQuery:r,isSearchable:q,onSearchChange:V,searchFilter:E,emit:S,onUpdateInput:j,multiselectRef:h,get isEqual(){return b},computed:m,ref:y,watch:I,get Multiselect(){return T},get showErrorToast(){return z},get resolve(){return k}};return Object.defineProperty(B,"__isScriptSetup",{enumerable:!1,value:!0}),B}}),W={class:"tw-w-full small px-3"},G={key:2},P={class:"tw-w-full multiselect-single-label",style:{"z-index":"1"}},Q={key:2,class:"multiselect-single-label-text"},H={class:"multiselect-tag is-user"},J={key:2},K=["onClick"];function X(n,f,a,e,v,c){return p(),D(e.Multiselect,{ref:"multiselectRef",value:e.valueForMultiSelectComponent,onInput:e.onUpdateInput,options:e.isOptionsAFunction?e.lazyOptionsForMultiSelectComponent:e.optionsForMultiSelectComponent,"filter-results":!e.isOptionsAFunction,delay:e.isOptionsAFunction?500:void 0,object:"",mode:a.multiselect?"tags":"single",disabled:a.disabled,required:a.required,"close-on-select":!a.multiselect,searchable:e.isSearchable,searchFilter:e.searchFilter,onSearchChange:e.onSearchChange,placeholder:a.placeholder,canClear:!a.noClear,canDeselect:!1,"data-testid":a.dataTestid,clearOnSelect:!e.isOptionsAFunction,clearOnBlur:!e.isOptionsAFunction},{option:F(o=>[g("div",W,[n.$slots.option?u(n.$slots,"option",{key:0,option:e.getOptionValueById(o.option.label)}):n.$slots.selectedOption?u(n.$slots,"selectedOption",{key:1,option:e.getOptionValueById(o.option.label)}):(p(),O("span",G,_(o.option.label),1))])]),singlelabel:F(o=>[g("div",P,[n.$slots.selectedOption?u(n.$slots,"selectedOption",{key:0,option:e.getOptionValueById(o.value.label)}):n.$slots.option?u(n.$slots,"option",{key:1,option:e.getOptionValueById(o.value.label)}):(p(),O("span",Q,_(o.value.label),1))])]),tag:F(o=>[g("div",H,[n.$slots.selectedOption?u(n.$slots,"selectedOption",{key:0,option:e.getOptionValueById(o.option.label)}):n.$slots.option?u(n.$slots,"option",{key:1,option:e.getOptionValueById(o.option.label)}):(p(),O("span",J,_(o.option.label),1)),o.disabled?N("",!0):(p(),O("span",{key:3,class:"multiselect-tag-remove",onClick:d=>o.handleTagRemove(o.option,d)},f[0]||(f[0]=[g("span",{class:"multiselect-tag-remove-icon"},null,-1)]),8,K))])]),_:3},8,["value","options","filter-results","delay","mode","disabled","required","close-on-select","searchable","placeholder","canClear","data-testid","clearOnSelect","clearOnBlur"])}const x=R(U,[["render",X],["__file","MetaInputSelect.vue"]]);export{x as default};
