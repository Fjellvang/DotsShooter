import{d as v,c as w,aU as f,a8 as _,b as g,o as l,a as n,n as t,k as o,t as s,F as d,r as x,$ as u,w as T,p as O,_ as k}from"./index-B7jvAjdE.js";import j from"./MetaCountryCode-OZPd0ilg.js";import V from"./MetaTime-D0iYB_lr.js";import{M as N}from"./MetaLanguageLabel-CAF0iIvu.js";const h=v({__name:"OverviewList",props:{listTitle:{},icon:{default:"bar-chart"},items:{},sourceObject:{}},setup(p,{expose:r}){r();const a=p,i=f(),b=w(()=>a.items.filter(y=>i.doesHavePermission(y.displayPermission))),c={props:a,permissions:i,filteredItems:b,computed:w,get MetaCountryCode(){return j},get MetaTime(){return V},get MTextButton(){return _},get usePermissions(){return f},MetaLanguageLabel:N};return Object.defineProperty(c,"__isScriptSetup",{enumerable:!1,value:!0}),c}}),C={class:"tw-border-b tw-border-neutral-300 tw-pb-1 tw-font-bold"},M={class:"tw-text-red-500"};function m(p,r,a,i,b,c){const y=g("fa-icon");return l(),n("div",null,[t("div",C,[o(y,{icon:a.icon},null,8,["icon"]),t("span",null,s(a.listTitle),1)]),(l(!0),n(d,null,x(i.filteredItems,e=>(l(),n("div",{class:"py-1 px-0 tw-flex tw-justify-between tw-border-b tw-border-neutral-300",key:e.displayName},[e.displayType==="country"?(l(),n(d,{key:0},[t("div",null,s(e.displayName),1),o(i.MetaCountryCode,{isoCode:e.displayValue(a.sourceObject),class:u(e.displayValue(a.sourceObject)==="Unknown"?"tw-text-neutral-500 tw-italic":""),"show-name":""},null,8,["isoCode","class"])],64)):e.displayType==="currency"?(l(),n(d,{key:1},[t("div",null,s(e.displayName),1),t("div",null,"$"+s(e.displayValue(a.sourceObject).toFixed(2)),1)],64)):e.displayType==="datetime"?(l(),n(d,{key:2},[t("div",null,s(e.displayName),1),o(i.MetaTime,{date:e.displayValue(a.sourceObject),showAs:e.displayHint==="date"?e.displayHint:void 0},null,8,["date","showAs"])],64)):e.displayType==="language"?(l(),n(d,{key:3},[t("div",null,s(e.displayName),1),o(i.MetaLanguageLabel,{class:"tw-mt-0.5 tw-text-sm",language:e.displayValue(a.sourceObject),variant:"badge"},null,8,["language"])],64)):e.displayType==="number"?(l(),n(d,{key:4},[t("div",{class:u(e.displayHint==="highlightIfNonZero"&&e.displayValue(a.sourceObject)!==0?"tw-text-red-500":"")},s(e.displayName),3),t("div",{class:u(e.displayHint==="highlightIfNonZero"&&e.displayValue(a.sourceObject)!==0?"tw-text-red-500":"")},s(e.displayValue(a.sourceObject)),3)],64)):e.displayType==="text"?(l(),n(d,{key:5},[t("div",null,s(e.displayName),1),t("div",{class:u(e.displayHint==="monospacedText"?"tw-font-mono":e.displayValue(a.sourceObject)==="Unknown"?"tw-text-neutral-500 tw-italic":"")},s(e.displayValue(a.sourceObject)),3)],64)):e.displayType==="link"?(l(),n(d,{key:6},[t("div",null,s(e.displayName),1),o(i.MTextButton,{to:e.linkUrl?e.linkUrl(a.sourceObject):void 0,"disabled-tooltip":e.disabledTooltip?e.disabledTooltip(a.sourceObject):void 0,variant:e.displayValue(a.sourceObject)===void 0?"warning":"primary"},{default:T(()=>[O(s(e.displayValue(a.sourceObject)??"Undefined"),1)],void 0,!0),_:2},1032,["to","disabled-tooltip","variant"])],64)):(l(),n(d,{key:7},[r[0]||(r[0]=t("div",{class:"tw-text-red-500"},"Unknown displayType",-1)),t("div",M,s(e.displayType),1)],64))]))),128))])}const P=k(h,[["render",m],["__file","OverviewList.vue"]]);export{P as O};
